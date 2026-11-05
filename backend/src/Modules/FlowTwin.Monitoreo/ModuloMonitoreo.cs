using FlowTwin.Monitoreo.Application;
using FlowTwin.Monitoreo.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Monitoreo;

public static class ModuloMonitoreo
{
    public static IServiceCollection AddModuloMonitoreo(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<EstadoPisoServicio>();
        services.AddSingleton<SuscripcionesPiso>();
        services.AddHostedService<NotificadorGemelo>();
        return services;
    }

    /// <summary>Estado del piso y hub del gemelo en vivo (sección API → Monitoreo, F-11). Alertas: siguiente paso.</summary>
    public static void MapMonitoreo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/pisos/{id:int}/estado", async (int id, EstadoPisoServicio s, CancellationToken ct) =>
                Results.Ok(await s.ParaUsuarioAsync(id, ct)))
            .WithTags("Monitoreo").RequireAuthorization()
            .WithSummary("Ocupación por zona, KPI y atenciones en curso del piso (F-11)");

        app.MapHub<GemeloHub>("/hubs/gemelo");
    }
}
