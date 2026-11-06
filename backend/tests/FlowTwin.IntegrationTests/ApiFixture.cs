using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// Levanta la API completa en memoria contra una base SQL Server de pruebas (FlowTwin_Pruebas),
/// creada desde cero con las migraciones y los datos de demostración. Nunca toca la base de desarrollo.
/// Necesita el contenedor de SQL Server levantado (docker-compose up -d).
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Fabrica { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Misma cadena de conexión que en desarrollo, pero otra base. En tu máquina sale de los
        // user-secrets; en la integración continua (GitHub Actions), de variables de entorno.
        var config = new ConfigurationBuilder().AddUserSecrets<Program>(optional: true).AddEnvironmentVariables().Build();
        var cadena = config.GetConnectionString("FlowTwin")
            ?? throw new InvalidOperationException(
                "Configura ConnectionStrings:FlowTwin en los user-secrets de FlowTwin.Api o en la variable ConnectionStrings__FlowTwin.");
        // Se cambia la base con el constructor oficial, no reemplazando texto: así funciona escriba
        // la cadena "Database=" o "Initial Catalog=", y nunca apunta por error a la base de desarrollo.
        var cadenaPruebas = new SqlConnectionStringBuilder(cadena) { InitialCatalog = "FlowTwin_Pruebas" }.ConnectionString;

        Environment.SetEnvironmentVariable("ConnectionStrings__FlowTwin", cadenaPruebas);
        Environment.SetEnvironmentVariable("BaseDatos__MigrarAlIniciar", "true");
        Environment.SetEnvironmentVariable("BaseDatos__SembrarDemo", "true");
        Environment.SetEnvironmentVariable("LimitePeticiones__AuthPorMinuto", "1000");
        Environment.SetEnvironmentVariable("LimitePeticiones__IngestaPorMinuto", "10000");
        // El motor no corre solo: las pruebas lo llaman a mano (ProcesarAsync) para controlar el tiempo
        Environment.SetEnvironmentVariable("Presencia__MotorActivo", "false");
        // Las pruebas verifican la regla F-02 encendida (en appsettings está apagada por ahora)
        Environment.SetEnvironmentVariable("Contrasenas__ExigirCambioPrimerIngreso", "true");

        await using (var db = new FlowTwinDbContext(new DbContextOptionsBuilder<FlowTwinDbContext>().UseSqlServer(cadenaPruebas).Options))
            await db.Database.EnsureDeletedAsync();

        Fabrica = new WebApplicationFactory<Program>();
        _ = Fabrica.Server;   // arranca la API: aplica migraciones y siembra
    }

    public Task DisposeAsync()
    {
        Fabrica.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Un HttpClient con la sesión iniciada del usuario indicado (contraseña demo123).</summary>
    public async Task<HttpClient> ClienteDeAsync(string correo, string clave = "demo123")
    {
        var cliente = Fabrica.CreateClient();
        var r = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { correo, clave });
        r.EnsureSuccessStatusCode();
        var cuerpo = await r.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cuerpo.GetProperty("accessToken").GetString());
        return cliente;
    }

    /// <summary>Acceso directo a la base, para comprobar lo que quedó guardado.</summary>
    public async Task<T> EnBaseAsync<T>(Func<FlowTwinDbContext, Task<T>> consulta)
    {
        using var scope = Fabrica.Services.CreateScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<FlowTwinDbContext>());
    }
}

[CollectionDefinition("Api")]
public sealed class ColeccionApi : ICollectionFixture<ApiFixture>;

/// <summary>
/// Qué tarjeta de la semilla usa cada prueba. Todas comparten la misma base, así que dos pruebas que
/// usan la misma tarjeta se arruinan entre sí según el orden en que corran. Elegir aquí, nunca a mano
/// dentro de la prueba. Libres: T-01 a T-04, T-14 y T-15. La semilla las deja todas Disponibles (sin
/// historia inventada): si una prueba necesita otro estado, lo prepara con el flujo real.
/// </summary>
public static class TarjetasDePrueba
{
    public const string CicloCompleto = "T-05";      // AdmisionTests: entrega, devolución y desinfección
    public const string Deshacer = "6";              // AdmisionTests: T-06, escrita como número (el escáner la normaliza)
    public const string SinPermiso = "T-07";         // AdmisionTests: Gerencia no puede entregar
    public const string EstadoPiso = "T-08";         // EstadoPisoTests
    public const string Presencia = "T-09";          // PresenciaTests (emisor minor 9)
    public const string HubEnVivo = "T-10";          // GemeloHubTests
    public const string VistaPrevia = "T-13";        // AdmisionTests: la vista previa no cambia nada
    public const string PorDesinfectar = "T-11";     // AdmisionTests: se entrega y se devuelve para dejarla por desinfectar
    public const int SinAtencion = 12;               // PresenciaTests: emisor de una tarjeta Disponible (RN-12)
}
