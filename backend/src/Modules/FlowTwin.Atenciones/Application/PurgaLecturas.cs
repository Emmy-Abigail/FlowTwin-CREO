using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowTwin.Atenciones.Application;

/// <summary>
/// DD-11: borra las lecturas crudas de más de RetencionLecturasDias. Volumen estimado: 3 nodos × 1 lote
/// cada 2 s × hasta 15 emisores ≈ 22 lecturas/s ≈ 970 000 filas por jornada de 12 h ≈ 6,8 millones en 7 días.
/// Por eso se borra en lotes de <see cref="TamanoLote"/>: un solo DELETE de un millón de filas bloquearía
/// la tabla mientras la ingesta sigue escribiendo.
/// </summary>
public sealed class PurgaLecturasServicio(IAtencionesDb db, IReloj reloj, IOptions<OpcionesIngesta> opciones, ILogger<PurgaLecturasServicio> log)
{
    public const int TamanoLote = 5_000;

    /// <summary>Devuelve cuántas lecturas borró.</summary>
    public async Task<int> PurgarAsync(CancellationToken ct)
    {
        var limite = reloj.AhoraUtc.AddDays(-opciones.Value.RetencionLecturasDias);
        var total = 0;
        int borradas;
        do
        {
            // ExecuteDelete va directo a SQL sin pasar por el interceptor de registro, que prohíbe borrar
            // eventos inmutables: esta es la ÚNICA excepción permitida, definida en el diccionario de datos.
            borradas = await db.LecturasBle
                .Where(l => l.LeidaEn < limite)
                .OrderBy(l => l.Id)
                .Take(TamanoLote)
                .ExecuteDeleteAsync(ct);
            total += borradas;
        } while (borradas == TamanoLote);

        if (total > 0) log.LogInformation("Purga: {Total} lecturas anteriores a {Limite:yyyy-MM-dd HH:mm} UTC", total, limite);
        return total;
    }
}

/// <summary>Una vez al día, a la HoraPurga local, ejecuta la purga. Revisa cada 15 minutos si ya toca.</summary>
public sealed class PurgaLecturas(IServiceScopeFactory scopes, IReloj reloj, ZonaHoraria zona,
    IOptions<OpcionesIngesta> opciones, ILogger<PurgaLecturas> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        DateOnly? ultimaPurga = null;
        using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await temporizador.WaitForNextTickAsync(detener))
        {
            var ahora = zona.ALocal(reloj.AhoraUtc);
            if (ahora.Hour != opciones.Value.HoraPurga || ultimaPurga == DateOnly.FromDateTime(ahora)) continue;
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PurgaLecturasServicio>().PurgarAsync(detener);
                ultimaPurga = DateOnly.FromDateTime(ahora);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falló la purga de lecturas; se reintenta en 15 minutos");
            }
        }
    }
}
