using FlowTwin.Dispositivos.Application;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Dispositivos;

public static class ModuloDispositivos
{
    public static IServiceCollection AddModuloDispositivos(this IServiceCollection services)
    {
        services.AddScoped<TarjetasServicio>();
        services.AddScoped<ITarjetasAdmision>(sp => sp.GetRequiredService<TarjetasServicio>());
        return services;
    }

    /// <summary>Rutas /api/v1/tarjetas (sección API → Dispositivos). Nodos y registro de tarjetas: Avance 2 y 3.</summary>
    public static void MapDispositivos(this IEndpointRouteBuilder app)
    {
        var tarjetas = app.MapGroup("/api/v1/tarjetas").WithTags("Dispositivos");

        tarjetas.MapGet("/", async (TarjetasServicio s, CancellationToken ct) => Results.Ok(await s.ListarParaUsuarioAsync(ct)))
            .RequireAuthorization(Politicas.OperadorOAdmin)
            .WithSummary("Tarjetas por estado, de las sedes del usuario");

        tarjetas.MapPost("/{id:int}/desinfectar", async (int id, TarjetasServicio s, CancellationToken ct) =>
                Results.Ok(await s.DesinfectarAsync(id, ct)))
            .RequireAuthorization(Politicas.SoloOperador)
            .WithSummary("Por desinfectar → Disponible (F-09)");

        tarjetas.MapPost("/{id:int}/extraviar", async (int id, TarjetasServicio s, CancellationToken ct) =>
                Results.Ok(await s.ExtraviarAsync(id, ct)))
            .RequireAuthorization(Politicas.OperadorOAdmin)
            .WithSummary("Sin devolver → Extraviada (F-10)");
    }
}
