using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Persistence;
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
        // Misma cadena de conexión que en desarrollo (user-secrets), pero otra base.
        var secretos = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        var cadena = secretos.GetConnectionString("FlowTwin")
            ?? throw new InvalidOperationException("Configura ConnectionStrings:FlowTwin en los user-secrets de FlowTwin.Api.");
        var cadenaPruebas = cadena.Replace("Database=FlowTwin;", "Database=FlowTwin_Pruebas;");

        Environment.SetEnvironmentVariable("ConnectionStrings__FlowTwin", cadenaPruebas);
        Environment.SetEnvironmentVariable("BaseDatos__MigrarAlIniciar", "true");
        Environment.SetEnvironmentVariable("BaseDatos__SembrarDemo", "true");
        Environment.SetEnvironmentVariable("LimitePeticiones__AuthPorMinuto", "1000");
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
