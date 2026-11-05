using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.IntegrationTests;

/// <summary>Registro y administración de nodos (F-15), de punta a punta.</summary>
[Collection("Api")]
public class NodosTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static Task<HttpResponseMessage> Registrar(HttpClient c, string codigo, string mac, int zonaId = 3) =>
        c.PostAsJsonAsync("/api/v1/nodos", new { codigo, mac, zonaId, posX = 20.0, posY = 16.0 });

    [Fact]
    public async Task Registrar_devuelve_la_clave_una_vez_y_en_la_base_solo_queda_su_hash()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var r = await Registrar(admin, "n50", "a4cf129b3e50");

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var cuerpo = await Json(r);
        var clave = cuerpo.GetProperty("clave").GetString()!;
        var nodo = cuerpo.GetProperty("nodo");
        Assert.Equal("N-50", nodo.GetProperty("codigo").GetString());
        Assert.Equal("A4:CF:12:9B:3E:50", nodo.GetProperty("mac").GetString());
        Assert.Equal("SIN_SENAL", nodo.GetProperty("estado").GetString());   // En línea llega con el primer latido

        var (hash, eventos, auditoria) = await api.EnBaseAsync(async db =>
        {
            var n = await db.Nodos.SingleAsync(x => x.Codigo == "N-50");
            return (n.ClaveApiHash,
                await db.EventosNodo.CountAsync(e => e.NodoId == n.Id),
                await db.Auditoria.Where(a => a.Entidad == "Nodo" && a.EntidadId == "N-50").Select(a => a.ValorDespues).SingleAsync());
        });
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(clave)), hash);
        Assert.Equal(1, eventos);
        Assert.DoesNotContain(clave, auditoria);   // la clave nunca va a la auditoría
    }

    [Fact]
    public async Task No_se_repite_la_MAC_de_otro_nodo()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var r = await Registrar(admin, "N-51", "020000000001");   // la de N-01 de la semilla

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("NODO_MAC_DUPLICADA", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Un_nodo_necesita_una_zona_que_exista_RN19()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var r = await Registrar(admin, "N-52", "A4:CF:12:9B:3E:52", zonaId: 999);

        Assert.Equal("RN19_ZONA_INEXISTENTE", (await Json(r)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task El_Operador_ve_los_nodos_pero_no_los_registra()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var lista = await Json(await operador.GetAsync("/api/v1/nodos"));
        var registro = await Registrar(operador, "N-53", "A4:CF:12:9B:3E:53");

        Assert.Contains(lista.EnumerateArray(), n => n.GetProperty("codigo").GetString() == "N-01");
        Assert.Equal(HttpStatusCode.Forbidden, registro.StatusCode);
    }

    [Fact]
    public async Task Regenerar_reemplaza_la_clave()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var creado = await Json(await Registrar(admin, "N-54", "A4:CF:12:9B:3E:54"));
        var id = creado.GetProperty("nodo").GetProperty("id").GetInt32();

        var nueva = (await Json(await admin.PostAsync($"/api/v1/nodos/{id}/regenerar-clave", null))).GetProperty("clave").GetString()!;

        Assert.NotEqual(creado.GetProperty("clave").GetString(), nueva);
        var hash = await api.EnBaseAsync(db => db.Nodos.Where(n => n.Id == id).Select(n => n.ClaveApiHash).SingleAsync());
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(nueva)), hash);
    }

    [Fact]
    public async Task Un_nodo_de_baja_sale_del_plano_y_ya_no_se_edita()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        var id = (await Json(await Registrar(admin, "N-55", "A4:CF:12:9B:3E:55"))).GetProperty("nodo").GetProperty("id").GetInt32();
        Assert.Contains(await NodosEnElPlano(admin), c => c == "N-55");

        var baja = await Json(await admin.PostAsync($"/api/v1/nodos/{id}/baja", null));
        var edicion = await admin.PutAsJsonAsync($"/api/v1/nodos/{id}", new { zonaId = 3, posX = 1, posY = 1, ajusteRssiDb = 0 });

        Assert.Equal("BAJA", baja.GetProperty("estado").GetString());
        Assert.DoesNotContain(await NodosEnElPlano(admin), c => c == "N-55");
        Assert.Equal(HttpStatusCode.Conflict, edicion.StatusCode);
    }

    [Fact]
    public async Task La_geometria_trae_los_nodos_de_la_semilla_con_su_posicion()
    {
        var gerencia = await api.ClienteDeAsync("gerencia@creo.pe");

        var piso = await Json(await gerencia.GetAsync("/api/v1/pisos/1/geometria"));

        var n02 = piso.GetProperty("nodos").EnumerateArray().Single(n => n.GetProperty("codigo").GetString() == "N-02");
        Assert.Equal(3, n02.GetProperty("zonaId").GetInt32());
        Assert.Equal(22.2m, n02.GetProperty("x").GetDecimal());
        Assert.Equal("SIN_SENAL", n02.GetProperty("estado").GetString());
    }

    private static async Task<List<string>> NodosEnElPlano(HttpClient c) =>
        [.. (await Json(await c.GetAsync("/api/v1/pisos/1/geometria"))).GetProperty("nodos").EnumerateArray()
            .Select(n => n.GetProperty("codigo").GetString()!)];
}
