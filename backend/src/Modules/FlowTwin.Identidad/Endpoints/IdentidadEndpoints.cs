using FlowTwin.Identidad.Application;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FlowTwin.Identidad.Endpoints;

/// <summary>Rutas /api/v1/auth y /api/v1/usuarios (sección API → Identidad).</summary>
public static class IdentidadEndpoints
{
    /// <summary>Nombre de la cookie del token de refresco. Solo viaja a /api/v1/auth.</summary>
    public const string CookieRefresco = "flowtwin_refresco";
    private const string RutaCookie = "/api/v1/auth";

    public static void MapIdentidad(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Autenticación");

        auth.MapPost("/login", async (LoginRequest req, AutenticacionServicio s, HttpContext http, CancellationToken ct) =>
                Respuesta(http, await s.IniciarSesionAsync(req, ct)))
            .AllowAnonymous().RequireRateLimiting("auth")
            .WithSummary("Inicia sesión (F-01)");

        auth.MapPost("/refresh", async (AutenticacionServicio s, HttpContext http, CancellationToken ct) =>
                Respuesta(http, await s.RefrescarAsync(http.Request.Cookies[CookieRefresco], ct)))
            .AllowAnonymous().RequireRateLimiting("auth")
            .WithSummary("Renueva el token de acceso con la cookie de refresco (F-03)");

        auth.MapPost("/logout", async (AutenticacionServicio s, HttpContext http, CancellationToken ct) =>
            {
                await s.CerrarSesionAsync(http.Request.Cookies[CookieRefresco], ct);
                http.Response.Cookies.Delete(CookieRefresco, new CookieOptions { Path = RutaCookie });
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithSummary("Cierra la sesión y revoca el token de refresco (F-03)");

        auth.MapPost("/cambiar-contrasena", async (CambiarContrasenaRequest req, AutenticacionServicio s, HttpContext http, CancellationToken ct) =>
                Respuesta(http, await s.CambiarContrasenaAsync(req, ct)))
            .RequireAuthorization()
            .WithSummary("Cambia la contraseña; obligatorio en el primer ingreso (F-02)");

        auth.MapGet("/yo", async (AutenticacionServicio s, CancellationToken ct) => Results.Ok(await s.YoAsync(ct)))
            .RequireAuthorization()
            .WithSummary("Datos, rol y pisos del usuario con sesión");

        var usuarios = app.MapGroup("/api/v1/usuarios").WithTags("Usuarios").RequireAuthorization(Politicas.SoloAdmin);

        usuarios.MapGet("/", async (bool? incluirInactivos, UsuariosServicio s, CancellationToken ct) =>
                Results.Ok(await s.ListarAsync(incluirInactivos ?? false, ct)))
            .WithSummary("Lista los usuarios; incluirInactivos=true muestra también los dados de baja");

        usuarios.MapPost("/", async (GuardarUsuarioRequest req, UsuariosServicio s, CancellationToken ct) =>
            {
                var creado = await s.CrearAsync(req, ct);
                return Results.Created($"/api/v1/usuarios/{creado.Usuario.Id}", creado);
            })
            .WithSummary("Crea un usuario y devuelve su contraseña temporal una sola vez (F-04)");

        usuarios.MapPut("/{id:int}", async (int id, GuardarUsuarioRequest req, UsuariosServicio s, CancellationToken ct) =>
                Results.Ok(await s.EditarAsync(id, req, ct)))
            .WithSummary("Edita nombre, correo, rol y pisos");

        usuarios.MapPost("/{id:int}/restablecer-contrasena", async (int id, UsuariosServicio s, CancellationToken ct) =>
                Results.Ok(await s.RestablecerAsync(id, ct)))
            .WithSummary("Genera una nueva contraseña temporal (F-05)");

        usuarios.MapPost("/{id:int}/desactivar", async (int id, UsuariosServicio s, CancellationToken ct) =>
            {
                await s.DesactivarAsync(id, ct);
                return Results.NoContent();
            })
            .WithSummary("Da de baja al usuario: EstadoRegistro = 0 y cierra sus sesiones (F-05)");

        usuarios.MapPost("/{id:int}/activar", async (int id, UsuariosServicio s, CancellationToken ct) =>
            {
                await s.ActivarAsync(id, ct);
                return Results.NoContent();
            })
            .WithSummary("Reactiva a un usuario dado de baja");
    }

    /// <summary>Devuelve el token de acceso en el cuerpo y deja el de refresco en una cookie HttpOnly (ADR-13).</summary>
    private static IResult Respuesta(HttpContext http, SesionIniciada sesion)
    {
        http.Response.Cookies.Append(CookieRefresco, sesion.TokenRefresco, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RutaCookie,
            Expires = sesion.RefrescoExpiraEn,
        });
        return Results.Ok(new { accessToken = sesion.AccessToken, expiraEn = sesion.ExpiraEn, usuario = sesion.Usuario });
    }
}
