using System.Threading.RateLimiting;
using FlowTwin.Api.Infraestructura;
using FlowTwin.Atenciones;
using FlowTwin.Auditoria;
using FlowTwin.Dispositivos;
using FlowTwin.Gemelo;
using FlowTwin.Identidad;
using FlowTwin.Identidad.Application;
using FlowTwin.Identidad.Endpoints;
using FlowTwin.Monitoreo;
using FlowTwin.Persistence;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

// =====================================================================
// FlowTwin.Api: la única puerta de entrada del sistema (ADR-01).
// 1) Registra servicios: módulos, base de datos, seguridad.
// 2) Arma la cadena de middlewares, en el orden de la sección "Recorrido de una petición".
// 3) Publica los endpoints de cada módulo.
// =====================================================================

var builder = WebApplication.CreateBuilder(args);

// ---- Logs técnicos con Serilog: consola y un archivo por día en logs/ (30 días)
builder.Host.UseSerilog((ctx, log) => log
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/flowtwin-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));

// ---- Servicios comunes
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();
builder.Services.AddSingleton<IReloj, RelojSistema>();
builder.Services.AddSingleton(new ZonaHoraria(builder.Configuration["ZonaHoraria"] ?? "America/Lima"));
builder.Services.AddSingleton<IEmisorTokens, EmisorTokensJwt>();
// Los 401/403/429 que genera ASP.NET (sin token, rol sin permiso, límite de peticiones) también
// salen como ProblemDetails con su código (sección "Traducción de errores").
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    var (codigo, titulo) = ctx.ProblemDetails.Status switch
    {
        401 => ("NO_AUTENTICADO", "Inicia sesión para continuar."),
        403 => ("SIN_PERMISO", "No tienes permiso para esta acción."),
        429 => ("BLOQUEADO", "Demasiados intentos. Espera un momento."),
        _ => (null, null),
    };
    if (codigo is not null && !ctx.ProblemDetails.Extensions.ContainsKey("codigo"))
    {
        ctx.ProblemDetails.Extensions["codigo"] = codigo;
        ctx.ProblemDetails.Title = titulo;
    }
    ctx.ProblemDetails.Extensions.TryAdd("traceId", System.Diagnostics.Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);
});
builder.Services.AddExceptionHandler<ManejadorErrores>();
builder.Services.AddOpenApi();

// ---- Base de datos y módulos
builder.Services.AddPersistencia(builder.Configuration);
builder.Services.AddModuloIdentidad(builder.Configuration);
builder.Services.AddModuloGemelo();
builder.Services.AddModuloDispositivos(builder.Configuration);
builder.Services.AddModuloAtenciones(builder.Configuration);
builder.Services.AddModuloAuditoria();
builder.Services.AddModuloMonitoreo();

// ---- Autenticación JWT y políticas por rol (sección Seguridad)
var jwt = builder.Configuration.GetSection("Jwt").Get<OpcionesJwt>() ?? new OpcionesJwt();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // conserva "sub", "rol", "piso" tal como se firmaron
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emisor,
            ValidAudience = jwt.Emisor,
            IssuerSigningKey = EmisorTokensJwt.Clave(jwt),
            NameClaimType = "sub",
            RoleClaimType = Claims.Rol,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // SignalR por WebSocket no puede enviar la cabecera Authorization: el navegador manda el token
        // en ?access_token=…, y SOLO se acepta en /hubs. El log de peticiones registra la ruta sin la
        // consulta, así el token no queda escrito en los logs.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = ctx.Request.Query["access_token"];
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.SoloAdmin, p => p.RequireClaim(Claims.Rol, "ADMIN"))
    .AddPolicy(Politicas.SoloOperador, p => p.RequireClaim(Claims.Rol, "OPERADOR"))
    .AddPolicy(Politicas.OperadorOAdmin, p => p.RequireClaim(Claims.Rol, "OPERADOR", "ADMIN"));

// ---- Límite de peticiones en /auth: frena a quien intenta adivinar contraseñas
var porMinuto = builder.Configuration.GetValue("LimitePeticiones:AuthPorMinuto", 20);
var ingestaPorMinuto = builder.Configuration.GetValue("LimitePeticiones:IngestaPorMinuto", 600);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Retry-After (estándar HTTP) le dice al cliente cuántos segundos esperar: el ESP32 lo usa
    // en vez de adivinar (docs/contrato-integracion-nodos.md)
    o.OnRejected = (contexto, _) =>
    {
        var segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera) ? (int)Math.Ceiling(espera.TotalSeconds) : 60;
        contexto.HttpContext.Response.Headers.RetryAfter = segundos.ToString();
        return ValueTask.CompletedTask;
    };
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = porMinuto, Window = TimeSpan.FromMinutes(1) }));
    // Ingesta, por IP (en la clínica cada ESP32 tiene la suya). No por la clave de la cabecera: quien
    // inventa una clave distinta en cada petición tendría un límite nuevo cada vez. Un nodo normal hace
    // ~32 peticiones/min; el margen cubre el reenvío de 10 minutos de lotes tras un corte de Wi-Fi.
    o.AddPolicy("ingesta", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = ingestaPorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// ---- Base de datos en desarrollo: aplica las migraciones y siembra los datos de demostración
if (app.Configuration.GetValue<bool>("BaseDatos:MigrarAlIniciar"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<FlowTwinDbContext>().Database.MigrateAsync();
}
if (app.Configuration.GetValue<bool>("BaseDatos:SembrarDemo"))
    await SemillaDesarrollo.SembrarAsync(app.Services);
// En todos los ambientes: si no hay ningún Administrador activo, crea el de "AdminInicial"
await AdminInicial.AsegurarAsync(app.Services);

// ---- Middlewares, en este orden (sección "Recorrido de una petición")
app.UseExceptionHandler();
app.UseStatusCodePages();             // cuerpo ProblemDetails para 401/403/429 sin cuerpo
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseMiddleware<CambioContrasenaObligatorio>();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                 // /openapi/v1.json
    app.MapScalarApiReference(o => o.WithTitle("CREO FlowTwin · API"));   // /scalar
}

// ---- Endpoints de cada módulo
app.MapGet("/api/v1/salud", () => Results.Ok(new { estado = "ok" })).AllowAnonymous().WithTags("Sistema");
app.MapIdentidad();
app.MapGemelo();
app.MapDispositivos();
app.MapAtenciones();
app.MapMonitoreo();

app.Run();

/// <summary>Visible para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program;
