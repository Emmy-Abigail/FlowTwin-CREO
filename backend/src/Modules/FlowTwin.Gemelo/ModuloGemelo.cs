using FlowTwin.SharedKernel;
using FlowTwin.Gemelo.Application;
using FlowTwin.Gemelo.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Gemelo;

public static class ModuloGemelo
{
    public static IServiceCollection AddModuloGemelo(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OpcionesMaquetas>(config.GetSection("Maquetas"));
        services.AddSingleton<IAlmacenMaquetas, AlmacenMaquetasEnDisco>();
        services.AddScoped<MaquetasServicio>();
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

        // Maqueta 3D (ADR-28): desde la carpeta del servidor y con sesión, no desde los archivos públicos del frontend
        pisos.MapGet("/{id:int}/maqueta", async (int id, MaquetasServicio s, CancellationToken ct) => Results.Ok(await s.InfoAsync(id, ct)))
            .WithSummary("Si el piso tiene maqueta 3D y qué ambientes pinta cada zona");
        pisos.MapGet("/{id:int}/maqueta/modelo.glb", (int id, MaquetasServicio s, CancellationToken ct) => Archivo(id, ArchivoMaqueta.Modelo, s, ct))
            .WithSummary("El modelo 3D del piso (.glb)");
        pisos.MapGet("/{id:int}/maqueta/ambientes.json", (int id, MaquetasServicio s, CancellationToken ct) => Archivo(id, ArchivoMaqueta.Ambientes, s, ct))
            .WithSummary("Los ambientes del plano que contiene el modelo");

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

    /// <summary>
    /// Results.Stream con ETag y fecha: si el navegador ya tiene esa versión responde 304 sin reenviar el
    /// archivo. «private»: puede guardarlo el navegador del usuario, no un proxy compartido (requiere sesión).
    /// </summary>
    private static async Task<IResult> Archivo(int id, ArchivoMaqueta archivo, MaquetasServicio s, CancellationToken ct)
    {
        var a = await s.ArchivoAsync(id, archivo, ct);
        return new ConCache(Results.Stream(a.Contenido, a.TipoMime, lastModified: a.Modificado, entityTag: a.Etiqueta));
    }

    private sealed class ConCache(IResult interno) : IResult
    {
        public Task ExecuteAsync(HttpContext http)
        {
            http.Response.Headers.CacheControl = "private, no-cache";
            return interno.ExecuteAsync(http);
        }
    }
}
