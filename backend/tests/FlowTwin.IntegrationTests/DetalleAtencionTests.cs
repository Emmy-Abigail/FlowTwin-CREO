using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Gemelo.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// GET /atenciones/{id} (P6). Las atenciones se arman ya cerradas directo en la base: así no ocupan
/// ninguna tarjeta (una atención cerrada no la deja En uso) y no chocan con las otras pruebas.
/// </summary>
[Collection("Api")]
public class DetalleAtencionTests(ApiFixture api)
{
    private const int Admision = 1, Sala = 3, Modulo2 = 7;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Una atención devuelta: Admisión 2 min, Sala 16, Módulo 2 14 (espera 18, consulta 14, total 33).</summary>
    private Task<long> AtencionCerradaAsync(string codigo, int? sedeId = null) => api.EnBaseAsync(async db =>
    {
        var inicio = DateTime.UtcNow.AddHours(-2);
        var tarjeta = await db.Tarjetas.SingleAsync(t => t.Codigo == "T-12");
        var sede = sedeId ?? tarjeta.SedeId;
        var a = Atencion.Abrir(codigo, DateOnly.FromDateTime(inicio), sede, tarjeta.Id, inicio);
        a.Cerrar(MotivoCierreAtencion.Devuelta, inicio.AddMinutes(33));
        db.Atenciones.Add(a);
        await db.SaveChangesAsync();
        db.EventosZona.AddRange(
            new EventoZona { AtencionId = a.Id, ZonaId = Admision, EntradaEn = inicio, SalidaEn = inicio.AddMinutes(2) },
            new EventoZona { AtencionId = a.Id, ZonaId = Sala, EntradaEn = inicio.AddMinutes(2), SalidaEn = inicio.AddMinutes(18) },
            new EventoZona { AtencionId = a.Id, ZonaId = Modulo2, EntradaEn = inicio.AddMinutes(18), SalidaEn = inicio.AddMinutes(32) });
        await db.SaveChangesAsync();
        return a.Id;
    });

    [Fact]
    public async Task El_detalle_trae_tiempos_y_recorrido_con_los_nombres_de_las_zonas()
    {
        var id = await AtencionCerradaAsync("A-9601");

        var r = await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync($"/api/v1/atenciones/{id}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var d = await Json(r);
        Assert.Equal("A-9601", d.GetProperty("codigo").GetString());
        Assert.Equal("T-12", d.GetProperty("tarjeta").GetString());
        Assert.Equal("Sede Principal", d.GetProperty("sede").GetString());
        Assert.Equal("FINALIZADA", d.GetProperty("estado").GetString());
        Assert.Equal("DEVUELTA", d.GetProperty("motivo").GetString());
        Assert.Equal(18, d.GetProperty("esperaMin").GetInt32());
        Assert.Equal(14, d.GetProperty("consultaMin").GetInt32());
        Assert.Equal(33, d.GetProperty("totalMin").GetInt32());
        var estadias = d.GetProperty("estadias").EnumerateArray().ToList();
        Assert.Equal(["Admisión y caja", "Sala de espera", "Módulo 2 (consultorio)"], estadias.Select(e => e.GetProperty("zona").GetString()));
        Assert.Equal(["INGRESO", "ESPERA", "SERVICIO"], estadias.Select(e => e.GetProperty("tipo").GetString()));
    }

    [Fact]
    public async Task Gerencia_tambien_lo_puede_ver_en_solo_lectura()
    {
        var id = await AtencionCerradaAsync("A-9602");

        var r = await (await api.ClienteDeAsync("gerencia@creo.pe")).GetAsync($"/api/v1/atenciones/{id}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Una_atencion_que_no_existe_da_404()
    {
        var r = await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync("/api/v1/atenciones/999999999");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Una_atencion_de_otra_sede_da_404_como_si_no_existiera_RN24()
    {
        var otraSede = await api.EnBaseAsync(async db =>
        {
            var s = new Sede { Nombre = "Sede de prueba P6", PrefijoAtencion = 'Z' };
            db.Sedes.Add(s);
            await db.SaveChangesAsync();
            return s.Id;
        });
        var id = await AtencionCerradaAsync("Z-9601", otraSede);

        var operador = await (await api.ClienteDeAsync("operador@creo.pe")).GetAsync($"/api/v1/atenciones/{id}");
        var admin = await (await api.ClienteDeAsync("admin@creo.pe")).GetAsync($"/api/v1/atenciones/{id}");

        Assert.Equal(HttpStatusCode.NotFound, operador.StatusCode);   // no revela que existe
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);            // el Administrador ve todas las sedes
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_ver()
    {
        var id = await AtencionCerradaAsync("A-9603");

        var r = await api.Fabrica.CreateClient().GetAsync($"/api/v1/atenciones/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
