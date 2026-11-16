using Microsoft.Extensions.FileProviders;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Sirve la aplicación web (el build de Angular) desde la misma API, en el mismo dominio. Así la cookie de sesión
/// sigue siendo SameSite=Strict, SignalR funciona sin CORS y no hay que abrir la API a otro origen. Solo se activa
/// si se configura Web:Ruta (en el contenedor); en desarrollo la web la sirve `ng serve` como siempre.
/// </summary>
public static class WebEstatica
{
    /// <summary>
    /// Política de contenido de la web: solo código propio. 'wasm-unsafe-eval' lo necesita el decodificador meshopt
    /// de la maqueta 3D (WebAssembly); 'unsafe-inline' en estilos, los estilos de cada componente de Angular; blob: en
    /// connect-src, las texturas de la maqueta, que Three.js descomprime en memoria y lee con fetch (no son conexiones
    /// a otro sitio). Las fuentes vienen de Google Fonts (pendiente: servirlas desde aquí para la instalación sin internet).
    /// </summary>
    public const string PoliticaContenido =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: blob:; connect-src 'self' blob:; worker-src 'self' blob:; object-src 'none'; " +
        "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>La carpeta del build si está configurada y tiene index.html; si no, null (no se sirve web).</summary>
    public static string? Carpeta(IConfiguration config, IHostEnvironment entorno)
    {
        var ruta = config["Web:Ruta"];
        if (string.IsNullOrWhiteSpace(ruta)) return null;
        var carpeta = Path.GetFullPath(Path.Combine(entorno.ContentRootPath, ruta));
        return File.Exists(Path.Combine(carpeta, "index.html")) ? carpeta : null;
    }

    /// <summary>Archivos con huella en el nombre (main-AB12CD.js): nunca cambian, el navegador los guarda un año.</summary>
    private static bool ConHuella(string nombre) => System.Text.RegularExpressions.Regex.IsMatch(nombre, @"-[A-Za-z0-9_]{8}\.(js|css)$");

    public static void UseWebEstatica(this WebApplication app, string carpeta)
    {
        var archivos = new PhysicalFileProvider(carpeta);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = archivos,
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl =
                ctx.File.Name == "index.html" ? "no-cache"
                : ConHuella(ctx.File.Name) ? "public, max-age=31536000, immutable"
                : "public, max-age=3600",
        });
    }

    /// <summary>
    /// Cualquier ruta de la app (/gemelo, /auditoria…) devuelve index.html y Angular dibuja la pantalla. Lo que es de
    /// la API o tiene extensión (un archivo que no existe) responde 404, no la página: así un error no se disfraza.
    /// </summary>
    public static void MapWebEstatica(this WebApplication app, string carpeta)
    {
        var indice = Path.Combine(carpeta, "index.html");
        app.MapFallback(async http =>
        {
            var ruta = http.Request.Path;
            if (ruta.StartsWithSegments("/api") || ruta.StartsWithSegments("/hubs") || Path.HasExtension(ruta.Value))
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            http.Response.ContentType = "text/html; charset=utf-8";
            http.Response.Headers.CacheControl = "no-cache";
            await http.Response.SendFileAsync(indice);
        }).AllowAnonymous();
    }
}
