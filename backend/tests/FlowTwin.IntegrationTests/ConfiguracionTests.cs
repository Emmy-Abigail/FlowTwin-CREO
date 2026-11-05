using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;

namespace FlowTwin.IntegrationTests;

/// <summary>GET /configuracion: el interruptor temporal de la simulación (ADR-22).</summary>
[Collection("Api")]
public class ConfiguracionTests(ApiFixture api)
{
    [Fact]
    public async Task En_desarrollo_la_simulacion_esta_disponible()
    {
        var r = await (await api.ClienteDeAsync("gerencia@creo.pe")).GetFromJsonAsync<JsonElement>("/api/v1/configuracion");

        Assert.True(r.GetProperty("simulacionDisponible").GetBoolean());
    }

    [Fact]
    public async Task En_la_clinica_la_simulacion_no_existe()
    {
        using var clinica = api.Fabrica.WithWebHostBuilder(b => b.UseSetting("Demo:SimulacionDisponible", "false"));
        var cliente = clinica.CreateClient();
        var login = await (await cliente.PostAsJsonAsync("/api/v1/auth/login", new { correo = "gerencia@creo.pe", clave = "demo123" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());

        var r = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/configuracion");

        Assert.False(r.GetProperty("simulacionDisponible").GetBoolean());
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_consultar()
    {
        var r = await api.Fabrica.CreateClient().GetAsync("/api/v1/configuracion");

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
