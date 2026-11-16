using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTwin.Mensajeria.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.IntegrationTests;

/// <summary>GET /salud: si la base y el broker responden, sin revelar errores internos.</summary>
[Collection("Api")]
public class SaludTests(ApiFixture api)
{
    [Fact]
    public async Task Con_la_base_y_el_broker_respondiendo_esta_sana_y_es_publica()
    {
        await api.Fabrica.Services.GetRequiredService<ServicioIngestaMqtt>().Conectado.WaitAsync(TimeSpan.FromSeconds(20));

        var r = await api.Fabrica.CreateClient().GetAsync("/api/v1/salud");   // sin sesión

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var d = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SANO", d.GetProperty("estado").GetString());
        Assert.Equal("SANO", d.GetProperty("componentes").GetProperty("baseDatos").GetString());
        Assert.Equal("SANO", d.GetProperty("componentes").GetProperty("broker").GetString());
    }

    [Fact]
    public async Task Sin_broker_responde_503_y_dice_que_componente_cayo_sin_detalles_internos()
    {
        // Una API apuntando a un puerto donde no hay broker (no toca al broker real ni a sus cuentas)
        using var sinBroker = api.Fabrica.WithWebHostBuilder(b => b.UseSetting("Mqtt:Puerto", "1999"));

        var r = await sinBroker.CreateClient().GetAsync("/api/v1/salud");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        var d = JsonSerializer.Deserialize<JsonElement>(texto);
        Assert.Equal("CAIDO", d.GetProperty("componentes").GetProperty("broker").GetString());
        Assert.Equal("SANO", d.GetProperty("componentes").GetProperty("baseDatos").GetString());
        Assert.DoesNotContain("1999", texto);   // ni servidores, ni puertos, ni excepciones
    }

    [Fact]
    public async Task Con_MQTT_apagado_a_proposito_esta_degradada_pero_responde_200()
    {
        using var sinMqtt = api.ConAjustes();

        var d = await sinMqtt.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/salud");

        Assert.Equal("DEGRADADO", d.GetProperty("estado").GetString());
    }
}
