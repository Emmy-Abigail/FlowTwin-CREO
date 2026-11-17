using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowTwin.Atenciones.Application;

/// <summary>Sección «Jornada» de appsettings. La hora del cierre está en Configuración › Parámetros (ADR-27).</summary>
public sealed class OpcionesJornada
{
    /// <summary>Apagado solo en las pruebas de integración: así no dependen de la hora a la que se corren.</summary>
    public bool CierreActivo { get; set; } = true;
}

/// <summary>
/// Cierres de atenciones sin devolución de la tarjeta (ADR-25):
///  - «sin devolver»: lo decide el Operador ante la alerta «sin lecturas». No es automático porque en el
///    piloto hay zonas sin nodo, donde un paciente que sigue en la clínica no se detecta;
///  - cierre de jornada (RN-08): automático; además vuelve a poner en servicio las zonas que quedaron fuera.
/// En los dos, la atención termina cuando se vio al paciente por última vez y la tarjeta pasa a Sin devolver.
/// </summary>
public sealed class CierreAtenciones(
    IAtencionesDb db, ITarjetasAdmision tarjetas, ISedesConsultas sedes, IAuditoria auditoria, IReloj reloj,
    ZonaHoraria zona, IParametrosSistema parametros, ILogger<CierreAtenciones> log)
{
    /// <summary>POST /atenciones/{id}/cerrar-sin-devolver. 404 si no es de las sedes del usuario (RN-24).</summary>
    public async Task CerrarSinDevolverAsync(long atencionId, CancellationToken ct)
    {
        var alcance = await sedes.SedesDelUsuarioAsync(ct);
        var atencion = await db.Atenciones.SingleOrDefaultAsync(a => a.Id == atencionId, ct);
        if (atencion is null || (alcance is not null && !alcance.Contains(atencion.SedeId)))
            throw new NoEncontradoException("La atención no existe.");
        if (!atencion.EstaAbierta)
            throw new ConflictoException($"La atención {atencion.Codigo} ya está cerrada.");

        await CerrarAsync(atencion, MotivoCierreAtencion.SinLecturas, "Cerrada sin devolver la tarjeta", ct);
        await db.GuardarAsync(ct);
    }

    /// <summary>
    /// Cierra lo que quedó abierto de días anteriores y, desde la hora de cierre, también lo de hoy.
    /// Es idempotente: se puede llamar cuantas veces se quiera (si el servidor estuvo apagado a la hora del
    /// cierre, lo hace al volver). Devuelve cuántas atenciones y zonas cerró.
    /// </summary>
    public async Task<(int Atenciones, int Zonas)> CerrarJornadaAsync(CancellationToken ct)
    {
        var ahora = reloj.AhoraUtc;
        var hoy = zona.FechaLocal(ahora);
        var cerrarHoy = zona.ALocal(ahora).Hour >= (await parametros.ObtenerAsync(ct)).HoraCierre;
        return await CerrarHastaAsync(cerrarHoy ? hoy : hoy.AddDays(-1), ct);   // fechas de Lima que ya terminaron su jornada
    }

    /// <summary>Cierra las atenciones abiertas con fecha hasta «hasta» (incluida) y las pausas que empezaron hasta ese día.</summary>
    public async Task<(int Atenciones, int Zonas)> CerrarHastaAsync(DateOnly hasta, CancellationToken ct)
    {
        var ahora = reloj.AhoraUtc;
        var atenciones = await db.Atenciones.Where(a => a.FinEn == null && a.FechaAtencion <= hasta).ToListAsync(ct);
        foreach (var a in atenciones)
            await CerrarAsync(a, MotivoCierreAtencion.CierreJornada, "Cierre de jornada", ct);

        var limite = zona.AUtc(hasta.AddDays(1), 0);   // fin del último día cerrado, en UTC
        var pausas = await db.PausasZona.Where(p => p.FinEn == null && p.InicioEn < limite).ToListAsync(ct);
        foreach (var p in pausas)
        {
            p.Finalizar(ahora);
            auditoria.Registrar(AccionAuditoria.Reanudar, "Zona", p.ZonaId.ToString(), new { motivo = p.MotivoPausaId.Codigo() }, new { por = "Cierre de jornada" });
        }

        if (atenciones.Count + pausas.Count > 0)
        {
            await db.GuardarAsync(ct);
            log.LogInformation("Cierre de jornada: {Atenciones} atenciones cerradas y {Zonas} zonas de vuelta en servicio", atenciones.Count, pausas.Count);
        }
        return (atenciones.Count, pausas.Count);
    }

    private async Task CerrarAsync(Atencion atencion, MotivoCierreAtencion motivo, string observacion, CancellationToken ct)
    {
        var estadias = await db.EventosZona.Where(e => e.AtencionId == atencion.Id).ToListAsync(ct);
        var fin = atencion.CerrarSinDevolucion(motivo, estadias.Max(e => e.SalidaEn));
        foreach (var abierta in estadias.Where(e => e.SalidaEn is null)) abierta.Cerrar(fin);
        await tarjetas.CambiarEstadoAsync(atencion.TarjetaId, EstadoTarjeta.SinDevolver, atencion.Id, observacion, ct);
        auditoria.Registrar(AccionAuditoria.Editar, "Atencion", atencion.Codigo,
            new { estado = "ABIERTA" }, new { estado = "CERRADA", motivo = motivo.Codigo(), finEn = fin });
    }
}

/// <summary>Cada 5 minutos revisa si toca el cierre de jornada (y recupera uno perdido si el servidor estuvo apagado).</summary>
public sealed class CierreJornada(IServiceScopeFactory scopes, IOptions<OpcionesJornada> opciones, ILogger<CierreJornada> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        if (!opciones.Value.CierreActivo) return;
        using var temporizador = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CierreAtenciones>().CerrarJornadaAsync(detener);
            }
            catch (Exception e) when (!detener.IsCancellationRequested)
            {
                log.LogError(e, "Falló el cierre de jornada; se reintenta en {Minutos} min", Intervalo.TotalMinutes);
            }
        } while (await temporizador.WaitForNextTickAsync(detener));
    }
}
