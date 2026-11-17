using FlowTwin.Reportes.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Reportes;

/// <summary>Consultas de lo que ya pasó: el detalle de una atención (P6) y el historial de un piso (P5).</summary>
public static class ModuloReportes
{
    public static IServiceCollection AddModuloReportes(this IServiceCollection services)
    {
        services.AddScoped<DetalleAtencionServicio>();
        services.AddScoped<HistorialServicio>();
        return services;
    }

    public static void MapReportes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/atenciones/{id:long}", async (long id, DetalleAtencionServicio s, CancellationToken ct) =>
                Results.Ok(await s.ParaUsuarioAsync(id, ct)))
            .RequireAuthorization().WithTags("Reportes")
            .WithSummary("Detalle de una atención: tiempos y recorrido (P6). 404 si no es de tus sedes (RN-24)");

        // P5 · Historial: todo es de un piso (RN-24) y de un periodo de días de Lima (máximo 31)
        var historial = app.MapGroup("/api/v1/pisos/{pisoId:int}/reportes").RequireAuthorization().WithTags("Reportes");
        historial.MapGet("/resumen", async (int pisoId, DateOnly desde, DateOnly hasta, HistorialServicio s, CancellationToken ct) =>
                Results.Ok(await s.ResumenAsync(pisoId, desde, hasta, ct)))
            .WithSummary("Atenciones del periodo y medianas de espera y tiempo total (P5)");
        historial.MapGet("/ocupacion", async (int pisoId, DateOnly desde, DateOnly hasta, int? zonaId, HistorialServicio s, CancellationToken ct) =>
                Results.Ok(await s.OcupacionAsync(pisoId, desde, hasta, zonaId, ct)))
            .WithSummary("Mapa día × hora de una zona: personas en promedio, con las horas sin datos (P5)");
        historial.MapGet("/atenciones", async (int pisoId, DateOnly desde, DateOnly hasta, string? codigo, int? pagina, int? tamano,
                HistorialServicio s, CancellationToken ct) =>
                Results.Ok(await s.AtencionesAsync(pisoId, desde, hasta, codigo, pagina, tamano, ct)))
            .WithSummary("Atenciones del periodo, de la más reciente a la más antigua, paginadas (P5)");
        historial.MapGet("/atenciones.csv", async (int pisoId, DateOnly desde, DateOnly hasta, string? codigo, HistorialServicio s, CancellationToken ct) =>
            {
                var (contenido, nombre) = await s.CsvAsync(pisoId, desde, hasta, codigo, ct);
                return Results.File(contenido, "text/csv; charset=utf-8", nombre);
            })
            .WithSummary("Las atenciones del periodo en CSV, anónimas. Queda registrado en la auditoría (P5)");
    }
}
