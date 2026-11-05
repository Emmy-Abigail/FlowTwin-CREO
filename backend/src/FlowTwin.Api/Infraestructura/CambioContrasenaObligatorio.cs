using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Primer ingreso (F-02): mientras el token diga debeCambiar=true, cualquier petición que no sea
/// cambiar la contraseña o cerrar sesión responde 403 CAMBIO_CONTRASENA_REQUERIDO.
/// </summary>
public sealed class CambioContrasenaObligatorio(RequestDelegate siguiente)
{
    private static readonly string[] Permitidas =
        ["/api/v1/auth/cambiar-contrasena", "/api/v1/auth/logout", "/api/v1/auth/yo", "/api/v1/auth/refresh"];

    public async Task InvokeAsync(HttpContext http)
    {
        var debeCambiar = http.User.Identity?.IsAuthenticated == true && http.User.FindFirst(Claims.DebeCambiar)?.Value == "true";
        if (debeCambiar && !Permitidas.Any(p => http.Request.Path.StartsWithSegments(p)))
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            var detalle = new ProblemDetails
            {
                Status = 403,
                Title = "Debes cambiar tu contraseña antes de continuar.",
                Type = "https://flowtwin.creo.local/errores/CAMBIO_CONTRASENA_REQUERIDO",
            };
            detalle.Extensions["codigo"] = "CAMBIO_CONTRASENA_REQUERIDO";
            await http.Response.WriteAsJsonAsync(detalle, options: null, contentType: "application/problem+json");
            return;
        }
        await siguiente(http);
    }
}
