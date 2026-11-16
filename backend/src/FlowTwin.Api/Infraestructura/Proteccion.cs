using System.Security.Claims;
using System.Threading.RateLimiting;
using FlowTwin.Auditoria.Domain;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Protección contra abuso y cabeceras de seguridad (informe de seguridad, OWASP ASVS nivel 1). En un solo lugar
/// para que se lea completa: límite de peticiones en TODA la API, tamaño máximo de una petición, cabeceras que
/// el navegador respeta y secretos obligatorios fuera de desarrollo.
/// </summary>
public static class Proteccion
{
    public sealed class Opciones
    {
        /// <summary>Peticiones por minuto que puede hacer un usuario (o una IP sin sesión) de forma sostenida.</summary>
        public int ApiPorMinuto { get; set; } = 300;
        /// <summary>Ráfaga permitida: cuántas puede hacer de golpe (al abrir una pantalla se piden varias cosas a la vez).</summary>
        public int ApiRafaga { get; set; } = 100;
        /// <summary>Tamaño máximo del cuerpo de una petición. La API no recibe archivos: 1 MB sobra.</summary>
        public long CuerpoMaximoBytes { get; set; } = 1_048_576;
    }

    public static Opciones Leer(IConfiguration config) => config.GetSection("LimitePeticiones").Get<Opciones>() ?? new Opciones();

    /// <summary>
    /// Límite global por «quién»: por usuario si tiene sesión (así toda una clínica detrás de una misma IP no se
    /// estorba entre sí) y por IP si no la tiene. Cubeta de fichas: permite la ráfaga de abrir una pantalla y luego
    /// un ritmo sostenido. Quien se pasa recibe 429 con Retry-After; los demás usuarios siguen atendidos.
    /// /salud y /auth tienen su propio límite, más estricto.
    /// </summary>
    public static PartitionedRateLimiter<HttpContext> LimiteGlobal(Opciones o) =>
        PartitionedRateLimiter.Create<HttpContext, string>(http =>
        {
            var ruta = http.Request.Path;
            if (ruta.StartsWithSegments("/api/v1/salud")) return RateLimitPartition.GetNoLimiter("salud");
            return RateLimitPartition.GetTokenBucketLimiter(Quien(http), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = o.ApiRafaga,
                TokensPerPeriod = Math.Max(1, o.ApiPorMinuto / 6),
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        });

    private static string Quien(HttpContext http)
    {
        var usuario = http.User.FindFirstValue("sub");
        return usuario is not null ? $"u:{usuario}" : $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "desconocida"}";
    }

    /// <summary>
    /// Rechaza con 413 una petición que anuncia un cuerpo demasiado grande, antes de leerla. El límite de Kestrel
    /// (en Program) cubre además los cuerpos sin longitud anunciada.
    /// </summary>
    public static IApplicationBuilder UseCuerpoMaximo(this IApplicationBuilder app, long maximo) =>
        app.Use(async (http, siguiente) =>
        {
            if (http.Request.ContentLength > maximo)
            {
                http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            await siguiente(http);
        });

    /// <summary>
    /// Cabeceras de seguridad (OWASP Secure Headers). A la API: nada de contenido ejecutable ni de ser incrustada en
    /// otra página, y sin caché (los datos son de la clínica). /scalar (solo en desarrollo) queda sin CSP para que
    /// su documentación funcione.
    /// </summary>
    public static IApplicationBuilder UseCabecerasSeguridad(this IApplicationBuilder app) =>
        app.Use(async (http, siguiente) =>
        {
            http.Response.OnStarting(() =>
            {
                var h = http.Response.Headers;
                h.XContentTypeOptions = "nosniff";
                h.XFrameOptions = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                // Otra web no puede incrustar ni leer nuestras respuestas (aviso de OWASP ZAP, regla 90004)
                h["Cross-Origin-Resource-Policy"] = "same-origin";
                if (http.Request.Path.StartsWithSegments("/api") || http.Request.Path.StartsWithSegments("/hubs"))
                {
                    h.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                    // Las maquetas ponen su propia caché (ETag); el resto de la API no se guarda en ningún lado
                    if (!h.ContainsKey("Cache-Control")) h.CacheControl = "no-store";
                }
                return Task.CompletedTask;
            });
            await siguiente(http);
        });

    /// <summary>
    /// Fuera de desarrollo, sin estos secretos la API NO arranca: es mejor un error claro al desplegar que una
    /// instalación que «funciona» sin sellar la bitácora o sin poder crear las cuentas de los nodos.
    /// </summary>
    public static void ExigirSecretos(IConfiguration config)
    {
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(config.GetConnectionString("FlowTwin"))) faltan.Add("ConnectionStrings__FlowTwin");
        if (config.GetValue("Auditoria:SelladoActivo", true) && SelladoAuditoria.LeerClave(config["Auditoria:ClaveSello"]) is null)
            faltan.Add("Auditoria__ClaveSello (base64, mínimo 32 bytes)");
        if (config.GetValue("Mqtt:Activo", true) && string.IsNullOrWhiteSpace(config["Mqtt:ClaveAdmin"])) faltan.Add("Mqtt__ClaveAdmin");
        if (faltan.Count > 0)
            throw new InvalidOperationException("Faltan secretos para arrancar fuera de desarrollo: " + string.Join(", ", faltan) + ". Van como variables de entorno.");
    }
}
