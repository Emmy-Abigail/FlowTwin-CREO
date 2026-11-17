using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Respuesta de /api/v1/salud: { "estado": "SANO", "componentes": { "baseDatos": "SANO", "broker": "SANO" } }.
/// Solo estados, sin descripciones ni excepciones: la ruta es pública y un error interno puede revelar
/// nombres de servidores o de bases. El detalle queda en el log.
/// </summary>
public static class Salud
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Texto(HealthStatus estado) => estado switch
    {
        HealthStatus.Healthy => "SANO",
        HealthStatus.Degraded => "DEGRADADO",
        _ => "CAIDO",
    };

    public static Task EscribirAsync(HttpContext http, HealthReport reporte)
    {
        http.Response.ContentType = "application/json; charset=utf-8";
        http.Response.Headers.CacheControl = "no-store";
        return http.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            estado = Texto(reporte.Status),
            componentes = reporte.Entries.ToDictionary(e => e.Key, e => Texto(e.Value.Status)),
        }, Json));
    }
}
