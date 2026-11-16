using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FlowTwin.Mensajeria.Application;

/// <summary>
/// ¿Está el backend conectado al broker y escuchando a los nodos? Para /salud. Si MQTT está apagado a
/// propósito (Mqtt:Activo = false) la API funciona, pero no recibe datos de los nodos: «degradado».
/// </summary>
public sealed class SaludBroker(ServicioIngestaMqtt ingesta, IOptions<OpcionesMqtt> opciones) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(
            !opciones.Value.Activo ? HealthCheckResult.Degraded("MQTT apagado por configuración.")
            : ingesta.Escuchando ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Sin conexión con el broker MQTT."));
}
