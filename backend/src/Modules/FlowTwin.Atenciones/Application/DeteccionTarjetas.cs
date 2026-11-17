using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowTwin.Atenciones.Application;

/// <summary>
/// Cada minuto toma las lecturas NUEVAS (por Id, como el motor) y le pasa a Dispositivos la última detección
/// de cada tarjeta vista. Así el inventario (P9) sabe qué tarjeta no se detecta hace tiempo sin recorrer
/// millones de lecturas en cada consulta. Es idempotente: reprocesar lecturas no cambia nada.
/// </summary>
public sealed class DeteccionTarjetasServicio(IAtencionesDb db, ITarjetasDeteccion tarjetas, ProgresoDeteccion progreso, IReloj reloj)
{
    /// <summary>Tope por vuelta: si el servidor estuvo apagado, se pone al día en varias vueltas sin un pico de memoria.</summary>
    public const int LoteMaximo = 50_000;

    public async Task<int> ActualizarAsync(CancellationToken ct)
    {
        // Al arrancar se retoma desde 10 minutos atrás: lo anterior ya se procesó antes de apagar
        progreso.UltimoId ??= await db.LecturasBle.Where(l => l.LeidaEn < reloj.AhoraUtc.AddMinutes(-10)).MaxAsync(l => (long?)l.Id, ct) ?? 0;
        var desde = progreso.UltimoId.Value;
        var lecturas = await db.LecturasBle.AsNoTracking()
            .Where(l => l.Id > desde).OrderBy(l => l.Id).Take(LoteMaximo)
            .Select(l => new { l.Id, l.UuidBle, l.LeidaEn, l.NodoId })
            .ToListAsync(ct);
        if (lecturas.Count == 0) return 0;

        var detecciones = lecturas.GroupBy(l => l.UuidBle)
            .Select(g => g.MaxBy(l => l.LeidaEn)!)
            .Select(l => new DeteccionTarjeta(l.UuidBle, l.LeidaEn, l.NodoId))
            .ToList();
        var cambiadas = await tarjetas.RegistrarAsync(detecciones, ct);
        progreso.UltimoId = lecturas[^1].Id;
        return cambiadas;
    }
}

/// <summary>Hasta qué lectura se procesó (en memoria: al reiniciar se retoma con un margen, ver arriba).</summary>
public sealed class ProgresoDeteccion
{
    public long? UltimoId { get; set; }
}

public sealed class DeteccionTarjetas(IServiceScopeFactory scopes, ILogger<DeteccionTarjetas> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        using var temporizador = new PeriodicTimer(Intervalo);
        while (await temporizador.WaitForNextTickAsync(detener))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DeteccionTarjetasServicio>().ActualizarAsync(detener);
            }
            catch (Exception e) when (!detener.IsCancellationRequested)
            {
                log.LogError(e, "Falló la actualización de la última detección de las tarjetas; se reintenta en 1 min");
            }
        }
    }
}
