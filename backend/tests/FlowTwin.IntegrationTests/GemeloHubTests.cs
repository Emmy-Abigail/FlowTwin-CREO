using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// Hub /hubs/gemelo de punta a punta (F-11): un navegador simulado se une al piso y recibe los cambios.
/// Usa long polling sobre el servidor de pruebas en memoria (el comportamiento del hub es el mismo).
/// </summary>
[Collection("Api")]
public class GemeloHubTests(ApiFixture api)
{
    private async Task<string> TokenDeAsync(string correo)
    {
        var r = await api.Fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = "demo123" });
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private HubConnection Conexion(string? token)
    {
        var servidor = api.Fabrica.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(servidor.BaseAddress, "/hubs/gemelo"), o =>
            {
                o.HttpMessageHandlerFactory = _ => servidor.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                if (token is not null) o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_conectar()
    {
        await using var conexion = Conexion(token: null);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => conexion.StartAsync());
    }

    [Fact]
    public async Task No_se_puede_mirar_un_piso_que_no_se_tiene_asignado_RN24()
    {
        await using var conexion = Conexion(await TokenDeAsync("gerencia@creo.pe"));   // solo tiene el piso 1
        await conexion.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => conexion.InvokeAsync("UnirsePiso", 2));
    }

    [Fact]
    public async Task Al_entregar_una_tarjeta_el_navegador_recibe_el_cambio_en_vivo()
    {
        var kpis = new ConcurrentQueue<JsonElement>();
        await using var conexion = Conexion(await TokenDeAsync("gerencia@creo.pe"));
        conexion.On<JsonElement>("KpisActualizados", k => kpis.Enqueue(k));
        await conexion.StartAsync();
        await conexion.InvokeAsync("UnirsePiso", 1);

        // Al unirse llega la foto completa
        var inicial = await EsperarAsync(kpis, _ => true);
        var enClinica = inicial.GetProperty("enClinica").GetInt32();

        // El Operador entrega una tarjeta: en menos de unos segundos llega el cambio
        var operador = await api.ClienteDeAsync("operador@creo.pe");
        await operador.PostAsJsonAsync("/api/v1/admision/escaneos", new { codigoQr = TarjetasDePrueba.HubEnVivo, modo = "AUTO" });
        var despues = await EsperarAsync(kpis, k => k.GetProperty("enClinica").GetInt32() == enClinica + 1);

        Assert.Equal(enClinica + 1, despues.GetProperty("enClinica").GetInt32());
        await operador.PostAsJsonAsync("/api/v1/admision/escaneos", new { codigoQr = TarjetasDePrueba.HubEnVivo, modo = "AUTO" });   // devuelve
    }

    /// <summary>Espera hasta 5 s un mensaje que cumpla la condición (el notificador envía cada 500 ms).</summary>
    private static async Task<JsonElement> EsperarAsync(ConcurrentQueue<JsonElement> mensajes, Func<JsonElement, bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < limite)
        {
            while (mensajes.TryDequeue(out var m))
                if (condicion(m)) return m;
            await Task.Delay(100);
        }
        throw new TimeoutException("No llegó el mensaje esperado por el hub en 5 s.");
    }
}
