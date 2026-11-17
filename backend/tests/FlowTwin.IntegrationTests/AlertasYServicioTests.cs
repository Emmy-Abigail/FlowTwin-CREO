using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Atenciones.Application;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// F5 y RF09 (ADR-25): zona fuera de servicio, alertas del Operador, cierre «sin devolver» y cierre de
/// jornada. Cada prueba crea su propia tarjeta (T-7nn) y su atención: no toca las 15 de la semilla.
/// </summary>
[Collection("Api")]
public class AlertasYServicioTests(ApiFixture api)
{
    private const int Piso1 = 1, Triaje = 4, Modulo2 = 7;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Una tarjeta En uso con su atención abierta, entregada hace «hace» minutos (fecha de Lima de esa hora).</summary>
    private Task<(long AtencionId, int TarjetaId, string Codigo)> AtencionAbiertaAsync(int numero, TimeSpan hace) => api.EnBaseAsync(async db =>
    {
        var tarjeta = new Tarjeta
        {
            Codigo = $"T-{numero}", CodigoQr = $"CREO-PRUEBA-{numero}", SedeId = 1,
            UuidBle = EmisorBle.Identificador(major: 1, minor: numero), EstadoTarjetaId = EstadoTarjeta.EnUso,
        };
        db.Tarjetas.Add(tarjeta);
        await db.SaveChangesAsync();
        var inicio = DateTime.UtcNow - hace;
        var codigo = $"A-{numero:D4}";
        var a = Atencion.Abrir(codigo, DateOnly.FromDateTime(inicio.AddHours(-5)), 1, tarjeta.Id, inicio);
        db.Atenciones.Add(a);
        await db.SaveChangesAsync();
        return (a.Id, tarjeta.Id, codigo);
    });

    // ---------- Zona fuera de servicio (RF09)
    [Fact]
    public async Task El_Operador_pone_una_zona_fuera_de_servicio_y_el_gemelo_lo_muestra()
    {
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var pausa = await operador.PostAsJsonAsync($"/api/v1/zonas/{Triaje}/pausas", new { motivo = "LIMPIEZA" });
        var otraVez = await operador.PostAsJsonAsync($"/api/v1/zonas/{Triaje}/pausas", new { motivo = "MANTENIMIENTO" });
        var estado = await Json(await operador.GetAsync($"/api/v1/pisos/{Piso1}/estado"));
        var reanudar = await operador.PostAsync($"/api/v1/zonas/{Triaje}/pausas/actual/finalizar", null);
        var reanudarOtraVez = await operador.PostAsync($"/api/v1/zonas/{Triaje}/pausas/actual/finalizar", null);

        Assert.Equal(HttpStatusCode.OK, pausa.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);   // RN-18: una a la vez
        var zona = estado.GetProperty("zonas").EnumerateArray().Single(z => z.GetProperty("zonaId").GetInt32() == Triaje);
        Assert.True(zona.GetProperty("pausada").GetBoolean());
        Assert.Equal("LIMPIEZA", zona.GetProperty("motivoPausa").GetString());
        Assert.Equal(HttpStatusCode.NoContent, reanudar.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reanudarOtraVez.StatusCode);
        Assert.True(await api.EnBaseAsync(db => db.Auditoria.AnyAsync(a => a.AccionAuditoriaId == AccionAuditoria.Pausar)));
        Assert.True(await api.EnBaseAsync(db => db.Auditoria.AnyAsync(a => a.AccionAuditoriaId == AccionAuditoria.Reanudar)));
    }

    [Fact]
    public async Task Gerencia_no_puede_y_un_motivo_que_no_existe_se_rechaza()
    {
        var gerencia = await (await api.ClienteDeAsync("gerencia@creo.pe"))
            .PostAsJsonAsync($"/api/v1/zonas/{Modulo2}/pausas", new { motivo = "LIMPIEZA" });
        var invalido = await (await api.ClienteDeAsync("operador@creo.pe"))
            .PostAsJsonAsync($"/api/v1/zonas/{Modulo2}/pausas", new { motivo = "VACACIONES" });
        var otroSinDetalle = await (await api.ClienteDeAsync("operador@creo.pe"))
            .PostAsJsonAsync($"/api/v1/zonas/{Modulo2}/pausas", new { motivo = "OTRO" });

        Assert.Equal(HttpStatusCode.Forbidden, gerencia.StatusCode);
        Assert.Equal("PAUSA_MOTIVO_INVALIDO", (await Json(invalido)).GetProperty("codigo").GetString());
        Assert.Equal("PAUSA_SIN_DETALLE", (await Json(otroSinDetalle)).GetProperty("codigo").GetString());
    }

    // ---------- Alertas y cierre «sin devolver»
    [Fact]
    public async Task Sin_lecturas_da_una_alerta_y_el_Operador_la_cierra_sin_devolver()
    {
        var (id, tarjetaId, codigo) = await AtencionAbiertaAsync(701, TimeSpan.FromMinutes(65));
        var operador = await api.ClienteDeAsync("operador@creo.pe");

        var alertas = (await Json(await operador.GetAsync($"/api/v1/pisos/{Piso1}/alertas")))
            .EnumerateArray().Where(a => a.GetProperty("codigo").GetString() == codigo).ToList();

        // Sin lecturas hace más de 30 min: una sola alerta, la que pide actuar (no también «65 min esperando»)
        Assert.Equal("TARJETA_SIN_DEVOLVER", Assert.Single(alertas).GetProperty("tipo").GetString());

        var cierre = await operador.PostAsync($"/api/v1/atenciones/{id}/cerrar-sin-devolver", null);
        var otraVez = await operador.PostAsync($"/api/v1/atenciones/{id}/cerrar-sin-devolver", null);
        var despues = await Json(await operador.GetAsync($"/api/v1/pisos/{Piso1}/alertas"));

        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
        Assert.DoesNotContain(despues.EnumerateArray(), a => a.GetProperty("codigo").GetString() == codigo);   // la alerta se fue sola
        var (motivo, finEn, inicioEn, estadoTarjeta) = await api.EnBaseAsync(async db =>
        {
            var a = await db.Atenciones.SingleAsync(x => x.Id == id);
            return (a.MotivoCierreId, a.FinEn, a.InicioEn, (await db.Tarjetas.SingleAsync(t => t.Id == tarjetaId)).EstadoTarjetaId);
        });
        Assert.Equal(MotivoCierreAtencion.SinLecturas, motivo);
        Assert.Equal(inicioEn, finEn);                         // nunca se la detectó: termina en la entrega, no «ahora»
        Assert.Equal(EstadoTarjeta.SinDevolver, estadoTarjeta);  // aparece en «Sin devolver» de Admisión
    }

    [Fact]
    public async Task Gerencia_no_ve_las_alertas_operativas()
    {
        var r = await (await api.ClienteDeAsync("gerencia@creo.pe")).GetAsync($"/api/v1/pisos/{Piso1}/alertas");

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ---------- Cierre de jornada (RN-08)
    [Fact]
    public async Task El_cierre_de_jornada_cierra_lo_que_quedo_abierto_de_dias_anteriores()
    {
        var (id, tarjetaId, _) = await AtencionAbiertaAsync(702, TimeSpan.FromDays(1) + TimeSpan.FromHours(2));
        var pausaId = await api.EnBaseAsync(async db =>
        {
            var p = PausaZona.Iniciar(Modulo2, MotivoPausa.Mantenimiento, null, UsuarioSistema.Id, DateTime.UtcNow.AddDays(-1));
            db.PausasZona.Add(p);
            await db.SaveChangesAsync();
            return p.Id;
        });

        using (var scope = api.Fabrica.Services.CreateScope())
            // Hasta AYER, fijo: así la prueba no depende de la hora y no cierra las atenciones de hoy de otras pruebas
            await scope.ServiceProvider.GetRequiredService<CierreAtenciones>()
                .CerrarHastaAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)).AddDays(-1), CancellationToken.None);

        var (motivo, tarjeta, pausaFin) = await api.EnBaseAsync(async db => (
            (await db.Atenciones.SingleAsync(a => a.Id == id)).MotivoCierreId,
            (await db.Tarjetas.SingleAsync(t => t.Id == tarjetaId)).EstadoTarjetaId,
            (await db.PausasZona.SingleAsync(p => p.Id == pausaId)).FinEn));
        Assert.Equal(MotivoCierreAtencion.CierreJornada, motivo);
        Assert.Equal(EstadoTarjeta.SinDevolver, tarjeta);
        Assert.NotNull(pausaFin);   // la zona volvió a estar en servicio
    }
}
