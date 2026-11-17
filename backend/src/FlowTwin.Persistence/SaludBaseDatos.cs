using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlowTwin.Persistence;

/// <summary>¿Responde SQL Server? Para /salud. Tiempo corto: un chequeo de salud nunca debe colgarse.</summary>
public sealed class SaludBaseDatos(FlowTwinDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            return await db.Database.CanConnectAsync(limite.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("La base no responde.");
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("La base no responde.", e);
        }
    }
}
