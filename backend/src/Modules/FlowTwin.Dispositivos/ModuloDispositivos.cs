using FlowTwin.Dispositivos.Application;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
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
        services.AddScoped<NodosServicio>();
        services.AddScoped<IUbicacionNodos, UbicacionNodos>();
        return services;
    }

    /// <summary>Rutas /api/v1/tarjetas y /api/v1/nodos (sección API → Dispositivos). Registro de tarjetas (F-16): más adelante.</summary>
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

        var nodos = app.MapGroup("/api/v1/nodos").WithTags("Dispositivos");

        nodos.MapGet("/", async (NodosServicio s, CancellationToken ct) => Results.Ok(await s.ListarAsync(ct)))
            .RequireAuthorization(Politicas.OperadorOAdmin)
            .WithSummary("Nodos y su estado (F-15)");

        nodos.MapPost("/", async (RegistrarNodoRequest req, NodosServicio s, CancellationToken ct) =>
            {
                var r = await s.RegistrarAsync(req, ct);
                return Results.Created($"/api/v1/nodos/{r.Nodo.Id}", r);
            })
            .RequireAuthorization(Politicas.SoloAdmin)
            .WithSummary("Registra un nodo y devuelve su clave, solo esta vez (F-15)");

        nodos.MapPut("/{id:int}", async (int id, EditarNodoRequest req, NodosServicio s, CancellationToken ct) =>
                Results.Ok(await s.EditarAsync(id, req, ct)))
            .RequireAuthorization(Politicas.SoloAdmin)
            .WithSummary("Cambia la zona, la posición o el ajuste de señal");

        nodos.MapPost("/{id:int}/regenerar-clave", async (int id, NodosServicio s, CancellationToken ct) =>
                Results.Ok(await s.RegenerarClaveAsync(id, ct)))
            .RequireAuthorization(Politicas.SoloAdmin)
            .WithSummary("Nueva clave; la anterior deja de servir");

        nodos.MapPost("/{id:int}/baja", async (int id, NodosServicio s, CancellationToken ct) =>
                Results.Ok(await s.DarDeBajaAsync(id, ct)))
            .RequireAuthorization(Politicas.SoloAdmin)
            .WithSummary("Retira el nodo e invalida su clave");
    }
}
