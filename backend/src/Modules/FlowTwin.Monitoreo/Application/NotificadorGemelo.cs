using FlowTwin.Monitoreo.Domain;
using FlowTwin.Monitoreo.Endpoints;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowTwin.Monitoreo.Application;

/// <summary>
/// Cada 500 ms (sección "Hub SignalR": los mensajes se agrupan cada 500 ms por piso), para cada piso que
/// alguien está mirando: calcula la foto, la compara con la anterior y envía solo lo que cambió.
///
/// Por qué comparar fotos y no "avisar" desde cada lugar que cambia algo: el motor, Admisión y el
/// vigilante no saben que existe SignalR (no hay acoplamiento ni nadie que olvide avisar), y si un
/// mensaje se pierde, la comparación siguiente lo corrige sola.
/// </summary>
public sealed class NotificadorGemelo(
    IServiceScopeFactory scopes, SuscripcionesPiso suscripciones, IHubContext<GemeloHub> hub, ILogger<NotificadorGemelo> log)
    : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(500);

    private readonly Dictionary<int, EstadoPiso> ultimas = [];

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        using var temporizador = new PeriodicTimer(Intervalo);
        while (await temporizador.WaitForNextTickAsync(detener))
        {
            try
            {
                await NotificarAsync(detener);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ultimas.Clear();   // la próxima vuelta reenvía todo y el navegador queda al día
                log.LogError(ex, "Falló el envío del estado del gemelo; se reintenta en {Intervalo} ms", Intervalo.TotalMilliseconds);
            }
        }
    }

    private async Task NotificarAsync(CancellationToken ct)
    {
        var mirados = suscripciones.PisosMirados();
        foreach (var piso in ultimas.Keys.Where(p => !mirados.Contains(p)).ToList()) ultimas.Remove(piso);
        if (mirados.Count == 0) return;

        using var scope = scopes.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<EstadoPisoServicio>();
        foreach (var pisoId in mirados)
        {
            var ahora = await servicio.CalcularAsync(pisoId, ct);
            if (ahora is null) continue;
            var grupo = hub.Clients.Group(GemeloHub.Grupo(pisoId));
            foreach (var m in Diferencias.Entre(ultimas.GetValueOrDefault(pisoId), ahora))
                await grupo.SendAsync(m.Metodo, m.Contenido, ct);
            ultimas[pisoId] = ahora;
        }
    }
}
