using FlowTwin.Atenciones.Application;
using FlowTwin.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Atenciones;

public static class ModuloAtenciones
{
    public static IServiceCollection AddModuloAtenciones(this IServiceCollection services)
    {
        services.AddScoped<AdmisionServicio>();
        services.AddValidatorsFromAssemblyContaining<EscaneoValidador>(ServiceLifetime.Singleton);
        return services;
    }

    /// <summary>Rutas /api/v1/admision (sección API → Atenciones e ingesta). Solo Operador.</summary>
    public static void MapAtenciones(this IEndpointRouteBuilder app)
    {
        var admision = app.MapGroup("/api/v1/admision").WithTags("Admisión").RequireAuthorization(Politicas.SoloOperador);

        admision.MapPost("/escaneos", async (EscaneoRequest req, AdmisionServicio s, CancellationToken ct) =>
                Results.Ok(await s.EscanearAsync(req, ct)))
            .WithSummary("Entrega o devolución según el estado de la tarjeta (F-06, F-08)");

        admision.MapPost("/atenciones/{id:long}/deshacer", async (long id, AdmisionServicio s, CancellationToken ct) =>
                Results.Ok(await s.DeshacerAsync(id, ct)))
            .WithSummary("Anula una entrega dentro de los 30 s (RN-04)");

        admision.MapGet("/resumen", async (AdmisionServicio s, CancellationToken ct) => Results.Ok(await s.ResumenAsync(ct)))
            .WithSummary("Conteo de tarjetas, pendientes y últimas operaciones");
    }
}
