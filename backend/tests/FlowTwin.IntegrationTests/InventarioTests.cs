using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Atenciones.Application;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>P9 · Dispositivos: inventario por excepción, alta por rango, baja y última detección. Tarjetas T-90nn propias.</summary>
[Collection("Api")]
public class InventarioTests(ApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static object Rango(int desde, int hasta) => new { sedeId = 1, desde, hasta };

    [Fact]
    public async Task Solo_el_Administrador_ve_el_inventario()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync("/api/v1/inventario/tarjetas?sedeId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.GetAsync("/api/v1/inventario/nodos")).StatusCode);
    }

    [Fact]
    public async Task Registrar_un_rango_crea_todas_o_ninguna_y_no_repite_codigos()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var r = await admin.PostAsJsonAsync("/api/v1/inventario/tarjetas", Rango(9001, 9003));
        var repetidas = await admin.PostAsJsonAsync("/api/v1/inventario/tarjetas", Rango(9003, 9005));
        var alReves = await admin.PostAsJsonAsync("/api/v1/inventario/tarjetas", Rango(10, 5));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var creadas = await Json(r);   // el cuerpo de una respuesta se lee una sola vez
        Assert.Equal(["T-9001", "T-9002", "T-9003"], creadas.EnumerateArray().Select(t => t.GetProperty("codigo").GetString()));
        Assert.Equal("CREO-T9001", creadas[0].GetProperty("codigoQr").GetString());
        Assert.Equal("TARJETAS_EXISTENTES", (await Json(repetidas)).GetProperty("codigo").GetString());
        Assert.False(await api.EnBaseAsync(db => db.Tarjetas.AnyAsync(t => t.Codigo == "T-9004")));   // ninguna del rango fallido
        Assert.Equal("RANGO_INVALIDO", (await Json(alReves)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Una_tarjeta_que_nunca_se_detecta_aparece_en_requieren_atencion_despues_de_un_dia()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        await admin.PostAsJsonAsync("/api/v1/inventario/tarjetas", Rango(9011, 9011));
        // Registrada hace dos días y nunca vista. ExecuteUpdate va directo a SQL: el interceptor de registro
        // (con razón) no deja reescribir la fecha de creación por EF
        await api.EnBaseAsync(db => db.Tarjetas.Where(x => x.Codigo == "T-9011")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaRegistro, DateTime.UtcNow.AddDays(-2))));

        var d = await Json(await admin.GetAsync("/api/v1/inventario/tarjetas?sedeId=1&buscar=9011"));

        var fila = d.GetProperty("filas").EnumerateArray().Single();
        Assert.Equal("NUNCA_DETECTADA", fila.GetProperty("problema").GetString());
        Assert.True(d.GetProperty("conteos").GetProperty("ATENCION").GetInt32() >= 1);
        Assert.True(d.GetProperty("conteos").GetProperty("TODAS").GetInt32() >= 16);
    }

    [Fact]
    public async Task Las_lecturas_nuevas_actualizan_la_ultima_deteccion_con_su_zona()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        await admin.PostAsJsonAsync("/api/v1/inventario/tarjetas", Rango(9021, 9021));
        var uuid = EmisorBle.Identificador(major: 1, minor: 9021);
        var vista = DateTime.UtcNow.AddSeconds(-30);
        await api.EnBaseAsync(async db =>
        {
            var nodo = await db.Nodos.SingleAsync(n => n.Codigo == "N-02");   // Sala de espera
            db.LecturasBle.Add(LecturaBle.Desde(nodo.Id, uuid, -60, 0, vista));
            return await db.SaveChangesAsync();
        });

        using (var scope = api.Fabrica.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DeteccionTarjetasServicio>().ActualizarAsync(CancellationToken.None);

        var fila = (await Json(await admin.GetAsync("/api/v1/inventario/tarjetas?sedeId=1&filtro=TODAS&buscar=9021")))
            .GetProperty("filas").EnumerateArray().Single();
        Assert.Equal("Sala de espera", fila.GetProperty("zonaUltimaDeteccion").GetString());
        Assert.Equal(JsonValueKind.Null, fila.GetProperty("problema").ValueKind);
    }

    [Fact]
    public async Task Se_da_de_baja_una_tarjeta_guardada_pero_no_una_que_tiene_un_paciente()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");
        await admin.PostAsJsonAsync("/api/v1/inventario/tarjetas", Rango(9031, 9032));
        var (libre, enUso) = await api.EnBaseAsync(async db =>
        {
            var t = await db.Tarjetas.SingleAsync(x => x.Codigo == "T-9032");
            t.EstadoTarjetaId = EstadoTarjeta.EnUso;
            await db.SaveChangesAsync();
            return ((await db.Tarjetas.SingleAsync(x => x.Codigo == "T-9031")).Id, t.Id);
        });

        var baja = await admin.PostAsync($"/api/v1/inventario/tarjetas/{libre}/baja", null);
        var conPaciente = await admin.PostAsync($"/api/v1/inventario/tarjetas/{enUso}/baja", null);

        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        Assert.Equal(EstadoTarjeta.Baja, await api.EnBaseAsync(db => db.Tarjetas.Where(t => t.Id == libre).Select(t => t.EstadoTarjetaId).SingleAsync()));
        Assert.Equal("RN_TRANSICION_INVALIDA", (await Json(conPaciente)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Los_nodos_traen_su_piso_y_zona_y_los_conteos_de_cada_filtro()
    {
        var d = await Json(await (await api.ClienteDeAsync("admin@creo.pe")).GetAsync("/api/v1/inventario/nodos?filtro=TODAS&buscar=n1"));

        var fila = d.GetProperty("filas").EnumerateArray().First(f => f.GetProperty("codigo").GetString() == "N-01");
        Assert.Equal("Admisión y caja", fila.GetProperty("zona").GetString());
        Assert.False(string.IsNullOrEmpty(fila.GetProperty("piso").GetString()));
        Assert.True(d.GetProperty("conteos").GetProperty("TODAS").GetInt32() >= 3);
    }
}
