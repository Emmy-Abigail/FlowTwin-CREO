using FlowTwin.Auditoria.Application;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Auditoria;

/// <summary>Registro de la bitácora (IAuditoria) y su consulta (P11, solo Administrador).</summary>
public static class ModuloAuditoria
{
    public static IServiceCollection AddModuloAuditoria(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OpcionesAuditoria>(config.GetSection("Auditoria"));
        services.AddScoped<IAuditoria, AuditoriaServicio>();
        services.AddScoped<AuditoriaConsultaServicio>();
        services.AddScoped<SelladoAuditoriaServicio>();
        services.AddHostedService<SelladoAuditoriaJob>();
        return services;
    }

    public static void MapAuditoria(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/auditoria").WithTags("Auditoría").RequireAuthorization(Politicas.SoloAdmin);

        grupo.MapGet("/", async (DateOnly desde, DateOnly hasta, int? usuarioId, string? categoria, string? buscar, int? pagina,
                AuditoriaConsultaServicio s, CancellationToken ct) =>
                Results.Ok(await s.ListarAsync(AuditoriaConsultaServicio.Validar(desde, hasta, usuarioId, categoria, buscar), pagina, ct)))
            .WithSummary("Bitácora del período, de la más reciente a la más antigua, paginada, con el conteo de cada categoría (P11)");

        grupo.MapGet("/{id:long}", async (long id, AuditoriaConsultaServicio s, CancellationToken ct) => Results.Ok(await s.DetalleAsync(id, ct)))
            .WithSummary("Un registro con su antes y después, la IP y el identificador de seguimiento");

        grupo.MapGet("/exportar.csv", async (DateOnly desde, DateOnly hasta, int? usuarioId, string? categoria, string? buscar,
                AuditoriaConsultaServicio s, CancellationToken ct) =>
            {
                var (contenido, nombre) = await s.CsvAsync(AuditoriaConsultaServicio.Validar(desde, hasta, usuarioId, categoria, buscar), ct);
                return Results.File(contenido, "text/csv; charset=utf-8", nombre);
            })
            .WithSummary("El filtro en CSV (máximo 10 000 filas). Queda registrado en la propia bitácora");

        grupo.MapPost("/integridad", async (DateOnly desde, DateOnly hasta, SelladoAuditoriaServicio sello, ZonaHoraria zona, CancellationToken ct) =>
            {
                AuditoriaConsultaServicio.Validar(desde, hasta, null, null, null);
                return Results.Ok(await sello.VerificarAsync(zona.AUtc(desde, 0), zona.AUtc(hasta.AddDays(1), 0), ct));
            })
            .WithSummary("Verifica la cadena de sellos de la bitácora del período");
    }
}
