using System.Collections.Concurrent;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// Un ESP32 de mentira para las pruebas: se conecta al broker real con el usuario y la clave del nodo,
/// publica en sus temas y espera la respuesta del backend, igual que lo hará el firmware (contrato v2.0).
/// </summary>
public sealed class NodoMqtt : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IMqttClient cliente;
    private readonly ConcurrentQueue<JsonElement> respuestas = new();
    private readonly SemaphoreSlim llegadas = new(0);

    public string Mac { get; }
    public string Raiz { get; }

    private NodoMqtt(IMqttClient cliente, string mac)
    {
        this.cliente = cliente;
        Mac = mac;
        Raiz = $"{ApiFixture.PrefijoMqtt}/v1/nodos/{mac.Replace(":", "").ToLowerInvariant()}";
    }

    public static string Usuario(string mac) => $"{ApiFixture.PrefijoMqtt}-{mac.Replace(":", "").ToLowerInvariant()}";

    /// <summary>Conecta y se suscribe a sus respuestas. Lanza ConexionRechazadaException si el broker lo rechaza.</summary>
    public static async Task<NodoMqtt> ConectarAsync(string mac, string clave, string? usuario = null)
    {
        var cliente = new MqttClientFactory().CreateMqttClient();
        var nodo = new NodoMqtt(cliente, mac);
        cliente.ApplicationMessageReceivedAsync += e =>
        {
            nodo.respuestas.Enqueue(JsonSerializer.Deserialize<JsonElement>(e.ApplicationMessage.ConvertPayloadToString()));
            nodo.llegadas.Release();
            return Task.CompletedTask;
        };
        var r = await cliente.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 1883).WithProtocolVersion(MqttProtocolVersion.V311)
            .WithClientId($"{usuario ?? Usuario(mac)}-{Guid.NewGuid():N}"[..40])
            .WithCredentials(usuario ?? Usuario(mac), clave).WithCleanSession(true).Build());
        if (r.ResultCode != MqttClientConnectResultCode.Success)
        {
            cliente.Dispose();
            throw new ConexionRechazadaException(r.ResultCode);
        }
        await cliente.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter($"{nodo.Raiz}/respuestas", MqttQualityOfServiceLevel.AtLeastOnce).Build());
        return nodo;
    }

    /// <summary>
    /// Las cuentas de los nodos de demostración las crea la sincronización al arrancar la API, en segundo
    /// plano: se reintenta unos segundos hasta que existan.
    /// </summary>
    public static async Task<NodoMqtt> ConectarCuandoExistaAsync(string mac, string clave)
    {
        for (var i = 0; ; i++)
        {
            try { return await ConectarAsync(mac, clave); }
            catch (ConexionRechazadaException) when (i < 40) { await Task.Delay(250); }
        }
    }

    public bool Conectado => cliente.IsConnected;

    public Task PublicarAsync(string tipo, object contenido, string? raiz = null) =>
        cliente.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic($"{raiz ?? Raiz}/{tipo}")
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(contenido, Json))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build());

    public Task PublicarCrudoAsync(string tipo, string texto) =>
        cliente.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"{Raiz}/{tipo}").WithPayload(texto)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build());

    /// <summary>La próxima respuesta del backend, o null si no llega a tiempo.</summary>
    public async Task<JsonElement?> RespuestaAsync(TimeSpan? espera = null)
    {
        if (!await llegadas.WaitAsync(espera ?? TimeSpan.FromSeconds(10))) return null;
        return respuestas.TryDequeue(out var r) ? r : null;
    }

    /// <summary>Publica y espera la respuesta: lo que hará el firmware antes de borrar el lote de su búfer.</summary>
    public async Task<JsonElement> EnviarAsync(string tipo, object contenido)
    {
        await PublicarAsync(tipo, contenido);
        return await RespuestaAsync() ?? throw new TimeoutException($"El backend no respondió al mensaje de {tipo}.");
    }

    public static object Lote(long secuencia, params (string uuid, int rssi, int edadMs)[] lecturas) =>
        new { secuencia, lecturas = lecturas.Select(l => new { uuidBle = l.uuid, l.rssi, l.edadMs }) };

    public async ValueTask DisposeAsync()
    {
        if (cliente.IsConnected) await cliente.DisconnectAsync();
        cliente.Dispose();
        llegadas.Dispose();
    }
}

/// <summary>El broker no aceptó la conexión (clave incorrecta, cuenta inexistente o borrada).</summary>
public sealed class ConexionRechazadaException(MqttClientConnectResultCode codigo) : Exception($"El broker rechazó la conexión: {codigo}")
{
    public MqttClientConnectResultCode Codigo { get; } = codigo;
}
