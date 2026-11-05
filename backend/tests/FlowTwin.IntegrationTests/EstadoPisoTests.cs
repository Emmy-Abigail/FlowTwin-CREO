using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Api.Infraestructura;
using FlowTwin.Atenciones.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.IntegrationTests;

/// <summary>GET /pisos/{id}/estado (F-11) de punta a punta. Usa N-03 (Módulo 2) y la tarjeta T-08.</summary>
[Collection("Api")]
public class EstadoPisoTests(ApiFixture api)
{
    private const int Hall = 2, Modulo2 = 7;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static JsonElement Zona(JsonElement estado, int id) =>
        estado.GetProperty("zonas").EnumerateArray().Single(z => z.GetProperty("zonaId").GetInt32() == id);

    [Fact]
    public async Task Un_paciente_en_Modulo_2_con_su_nodo_en_linea_se_ve_en_el_estado()
    {
        // N-03 (Módulo 2) envía un latido: queda En línea
        var nodo = api.Fabrica.CreateClient();
        nodo.DefaultRequestHeaders.Add("X-Node-Key", SemillaDesarrollo.ClaveNodoDemo("N-03"));
        await nodo.PostAsJsonAsync("/api/v1/ingesta/latidos", new { mac = "02:00:00:00:00:03" });

        // Se entrega una tarjeta y el motor lo ubicó en Módulo 2 hace 3 minutos
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        var entrega = await Json(await operador.PostAsJsonAsync("/api/v1/admision/escaneos", new { codigoQr = TarjetasDePrueba.EstadoPiso, modo = "AUTO" }));
        var codigo = entrega.GetProperty("atencion").GetProperty("codigo").GetString();
        var atencionId = entrega.GetProperty("atencion").GetProperty("id").GetInt64();
        await api.EnBaseAsync(async db =>
        {
            db.EventosZona.Add(new EventoZona { AtencionId = atencionId, ZonaId = Modulo2, EntradaEn = DateTime.UtcNow.AddMinutes(-3) });
            return await db.SaveChangesAsync();
        });

        var r = await (await api.ClienteDeAsync("gerencia@creo.pe")).GetAsync("/api/v1/pisos/1/estado");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var estado = await Json(r);
        var modulo2 = Zona(estado, Modulo2);
        Assert.True(modulo2.GetProperty("ocupacion").GetInt32() >= 1);
        Assert.NotEqual("SIN_DATOS", modulo2.GetProperty("nivel").GetString());
        var paciente = estado.GetProperty("atenciones").EnumerateArray().Single(a => a.GetProperty("codigo").GetString() == codigo);
        Assert.Equal("EN_ATENCION", paciente.GetProperty("estado").GetString());
        Assert.Equal(Modulo2, paciente.GetProperty("zonaActual").GetInt32());
        Assert.True(estado.GetProperty("kpis").GetProperty("enClinica").GetInt32() >= 1);

        await operador.PostAsJsonAsync("/api/v1/admision/escaneos", new { codigoQr = TarjetasDePrueba.EstadoPiso, modo = "AUTO" });   // devuelve
    }

    [Fact]
    public async Task El_Hall_no_tiene_semaforo_y_una_zona_sin_nodo_aparece_sin_datos()
    {
        var estado = await Json(await (await api.ClienteDeAsync("admin@creo.pe")).GetAsync("/api/v1/pisos/1/estado"));

        Assert.Equal(8, estado.GetProperty("zonas").GetArrayLength());
        Assert.Equal("NINGUNO", Zona(estado, Hall).GetProperty("nivel").GetString());
        Assert.Equal("SIN_DATOS", Zona(estado, 4).GetProperty("nivel").GetString());   // Triaje: no tiene nodo
    }

    [Fact]
    public async Task Un_piso_que_no_existe_responde_404()
    {
        var r = await (await api.ClienteDeAsync("admin@creo.pe")).GetAsync("/api/v1/pisos/999/estado");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
