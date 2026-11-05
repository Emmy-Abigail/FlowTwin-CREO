using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowTwin.Dispositivos.Application;

/// <summary>
/// Trabajo en segundo plano (sección "Trabajos en segundo plano"): cada RevisionLatidosSegundos pasa a
/// Sin señal los nodos callados (RN-20). Cada vuelta usa su propio scope, porque el DbContext no se
/// comparte entre hilos, y un error en una vuelta se registra sin detener al vigilante.
/// </summary>
public sealed class VigilanteLatidos(IServiceScopeFactory scopes, IOptions<OpcionesNodos> opciones, ILogger<VigilanteLatidos> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(opciones.Value.RevisionLatidosSegundos));
        while (await temporizador.WaitForNextTickAsync(detener))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IngestaNodosServicio>().RevisarLatidosAsync(detener);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falló la revisión de latidos; se reintenta en la próxima vuelta");
            }
        }
    }
}
