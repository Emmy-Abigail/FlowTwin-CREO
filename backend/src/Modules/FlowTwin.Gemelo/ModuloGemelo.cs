using FlowTwin.Gemelo.Application;
using FlowTwin.Gemelo.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Gemelo;

public static class ModuloGemelo
{
    public static IServiceCollection AddModuloGemelo(this IServiceCollection services)
    {
        services.AddScoped<GemeloServicio>();
        services.AddScoped<ISedesConsultas>(sp => sp.GetRequiredService<GemeloServicio>());
        return services;
    }

    public static void MapGemelo(this IEndpointRouteBuilder app)
    {
        var sedes = app.MapGroup("/api/v1/sedes").WithTags("Gemelo").RequireAuthorization();
        sedes.MapGet("/", async (GemeloServicio s, CancellationToken ct) => Results.Ok(await s.SedesVisiblesAsync(ct)))
            .WithSummary("Sedes y pisos publicados visibles para el usuario");
    }
}
