using System.Diagnostics;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Traduce cualquier excepción a ProblemDetails (RFC 9457) con un código estable y el traceId
/// (sección "Traducción de errores"). Un error no controlado nunca muestra detalles técnicos.
/// </summary>
public sealed class ManejadorErrores(IProblemDetailsService problemas, ILogger<ManejadorErrores> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception error, CancellationToken ct)
    {
        var (estado, codigo, titulo, datos) = error switch
        {
            ErrorFlowTwin e => (e.EstadoHttp, e.Codigo, e.Message, e.Datos),
            DbUpdateConcurrencyException => (409, "CONFLICTO", "Otro usuario cambió este dato mientras lo editabas. Recarga e intenta de nuevo.", null),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                (409, "CONFLICTO", "Otro usuario hizo la misma operación al mismo tiempo. Intenta de nuevo.", null),
            DbUpdateException { InnerException: SqlException { Number: 547 } } =>
                (400, "REFERENCIA_INVALIDA", "Uno de los datos hace referencia a algo que no existe.", null),
            BadHttpRequestException => (400, "VALIDACION", "La petición no tiene el formato esperado.", (object?)null),
            _ => (500, "ERROR_INTERNO", "Ocurrió un error inesperado. Comunica el traceId al equipo técnico.", null),
        };

        if (estado >= 500) log.LogError(error, "Error no controlado en {Ruta}", http.Request.Path);
        else log.LogInformation("{Codigo} en {Ruta}: {Mensaje}", codigo, http.Request.Path, titulo);

        http.Response.StatusCode = estado;
        var detalle = new ProblemDetails { Status = estado, Title = titulo, Type = $"https://flowtwin.creo.local/errores/{codigo}" };
        detalle.Extensions["codigo"] = codigo;
        detalle.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier;
        if (error is ValidacionException v) detalle.Extensions["errores"] = v.Errores;
        else if (datos is not null) detalle.Extensions["datos"] = datos;

        return await problemas.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = detalle, Exception = error });
    }
}
