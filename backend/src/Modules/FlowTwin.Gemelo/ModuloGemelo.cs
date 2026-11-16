using FlowTwin.SharedKernel;
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
        services.AddScoped<IZonasConsultas, ZonasConsultas>();
        services.AddSingleton<CacheParametros>();
        services.AddScoped<IParametrosSistema, ParametrosServicio>();
        services.AddScoped<ConfiguracionServicio>();
        return services;
    }

    public static void MapGemelo(this IEndpointRouteBuilder app)
    {
        var sedes = app.MapGroup("/api/v1/sedes").WithTags("Gemelo").RequireAuthorization();
        sedes.MapGet("/", async (GemeloServicio s, CancellationToken ct) => Results.Ok(await s.SedesVisiblesAsync(ct)))
            .WithSummary("Sedes y pisos publicados visibles para el usuario");

        var pisos = app.MapGroup("/api/v1/pisos").WithTags("Gemelo").RequireAuthorization();
        pisos.MapGet("/{id:int}/geometria", async (int id, GemeloServicio s, CancellationToken ct) => Results.Ok(await s.GeometriaAsync(id, ct)))
            .WithSummary("Geometría publicada del piso para las vistas 2D y 3D (F-11)");

        // Configuración (ADR-27): solo el Administrador
        var config = app.MapGroup("/api/v1/configuracion").WithTags("Configuración").RequireAuthorization(Politicas.SoloAdmin);
        config.MapGet("/arbol", async (ConfiguracionServicio s, CancellationToken ct) => Results.Ok(await s.ArbolAsync(ct)))
            .WithSummary("Sedes con todos sus pisos, publicados y en borrador");
        config.MapPost("/sedes", async (SedeRequest req, ConfiguracionServicio s, CancellationToken ct) => Results.Ok(await s.CrearSedeAsync(req, ct)));
        config.MapPut("/sedes/{id:int}", async (int id, SedeRequest req, ConfiguracionServicio s, CancellationToken ct) =>
            { await s.EditarSedeAsync(id, req, ct); return Results.NoContent(); });
        config.MapPost("/sedes/{id:int}/pisos", async (int id, PisoRequest req, ConfiguracionServicio s, CancellationToken ct) =>
            Results.Ok(await s.CrearPisoAsync(id, req, ct)));
        config.MapPut("/pisos/{id:int}", async (int id, PisoRequest req, ConfiguracionServicio s, CancellationToken ct) =>
            { await s.EditarPisoAsync(id, req, ct); return Results.NoContent(); });
        config.MapPost("/pisos/{id:int}/publicar", async (int id, ConfiguracionServicio s, CancellationToken ct) =>
            { await s.PublicarPisoAsync(id, ct); return Results.NoContent(); })
            .WithSummary("El piso aparece en el gemelo");
        config.MapGet("/pisos/{id:int}/zonas", async (int id, ConfiguracionServicio s, CancellationToken ct) => Results.Ok(await s.ZonasAsync(id, ct)));
        config.MapPost("/pisos/{id:int}/zonas", async (int id, ZonaRequest req, ConfiguracionServicio s, CancellationToken ct) =>
            Results.Ok(await s.CrearZonaAsync(id, req, ct)));
        config.MapPut("/zonas/{id:int}", async (int id, ZonaRequest req, ConfiguracionServicio s, CancellationToken ct) =>
            Results.Ok(await s.EditarZonaAsync(id, req, ct)));
        config.MapPost("/zonas/{id:int}/baja", async (int id, ConfiguracionServicio s, CancellationToken ct) =>
            { await s.DarDeBajaZonaAsync(id, ct); return Results.NoContent(); });
        config.MapGet("/parametros", async (ConfiguracionServicio s, CancellationToken ct) => Results.Ok(await s.ParametrosAsync(ct)));
        config.MapPut("/parametros", async (ParametrosRequest req, ConfiguracionServicio s, CancellationToken ct) =>
            Results.Ok(await s.GuardarParametrosAsync(req, ct)))
            .WithSummary("Alertas, cierre de jornada y semáforo de las salas de espera");
    }
}
