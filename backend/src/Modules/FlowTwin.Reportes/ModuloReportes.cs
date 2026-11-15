using FlowTwin.Reportes.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Reportes;

/// <summary>Consultas de lo que ya pasó: el detalle de una atención (P6) y, después, el historial (P5).</summary>
public static class ModuloReportes
{
    public static IServiceCollection AddModuloReportes(this IServiceCollection services)
    {
        services.AddScoped<DetalleAtencionServicio>();
        return services;
    }

    public static void MapReportes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/atenciones/{id:long}", async (long id, DetalleAtencionServicio s, CancellationToken ct) =>
                Results.Ok(await s.ParaUsuarioAsync(id, ct)))
            .RequireAuthorization().WithTags("Reportes")
            .WithSummary("Detalle de una atención: tiempos y recorrido (P6). 404 si no es de tus sedes (RN-24)");
    }
}
