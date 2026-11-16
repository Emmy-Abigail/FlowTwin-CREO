using System.Collections.Concurrent;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace FlowTwin.Simulador;

/// <summary>Qué hacer después de enviar algo como nodo, según la sección 6 del contrato.</summary>
public enum RespuestaNodo { Recibido, Rechazado, ClaveInvalida, Esperar, SinConexion }

/// <summary>Los mensajes de los nodos simulados. Interfaz para poder probar la simulación sin broker.</summary>
public interface ICanalNodos
{
    Task<(RespuestaNodo, TimeSpan)> LatidoAsync(NodoSimulado nodo, CancellationToken ct);
    Task<(RespuestaNodo, TimeSpan)> LoteAsync(NodoSimulado nodo, long secuencia, List<LecturaApi> lecturas, CancellationToken ct);
}

/// <summary>
/// Cada ESP32 simulado por MQTT (contrato v2.0), como lo hará el firmware: su propia conexión con su
/// usuario y clave, publica en sus temas con QoS 1 y espera la respuesta del backend antes de dar el
/// lote por enviado. No referencia el backend: si el contrato cambia sin avisar, el simulador lo delata.
/// </summary>
public sealed class NodosMqtt(ConexionMqtt config) : ICanalNodos, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan EsperaRespuesta = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, Conexion> conexiones = new();

    private sealed class Conexion(IMqttClient cliente)
    {
        public IMqttClient Cliente { get; } = cliente;
        public ConcurrentQueue<JsonElement> Respuestas { get; } = new();
        public SemaphoreSlim Llegadas { get; } = new(0);
    }

    public Task<(RespuestaNodo, TimeSpan)> LatidoAsync(NodoSimulado nodo, CancellationToken ct) =>
        EnviarAsync(nodo, "latidos", new { versionFirmware = "simulador-2.0", rssiWifi = -55, memoriaLibre = 150_000 }, ct);

    public Task<(RespuestaNodo, TimeSpan)> LoteAsync(NodoSimulado nodo, long secuencia, List<LecturaApi> lecturas, CancellationToken ct) =>
        EnviarAsync(nodo, "lecturas", new { secuencia, lecturas }, ct);

    private async Task<(RespuestaNodo, TimeSpan)> EnviarAsync(NodoSimulado nodo, string tipo, object cuerpo, CancellationToken ct)
    {
        Conexion c;
        try { c = await ConectadoAsync(nodo, ct); }
        catch (ConexionRechazada) { return (RespuestaNodo.ClaveInvalida, TimeSpan.FromSeconds(60)); }
        catch (Exception e) when (e is not OperationCanceledException) { return (RespuestaNodo.SinConexion, TimeSpan.FromSeconds(2)); }

        while (c.Respuestas.TryDequeue(out _)) { }   // respuestas viejas (de un envío que se dio por perdido)
        try
        {
            await c.Cliente.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic($"{config.Raiz(nodo.Mac)}/{tipo}")
                .WithPayload(JsonSerializer.SerializeToUtf8Bytes(cuerpo, Json))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException) { return (RespuestaNodo.SinConexion, TimeSpan.FromSeconds(2)); }

        if (!await c.Llegadas.WaitAsync(EsperaRespuesta, ct) || !c.Respuestas.TryDequeue(out var r))
            return (RespuestaNodo.SinConexion, TimeSpan.FromSeconds(2));   // sin respuesta: se reintenta con la misma secuencia
        return r.GetProperty("estado").GetString() switch
        {
            "GUARDADO" or "DUPLICADO" => (RespuestaNodo.Recibido, TimeSpan.Zero),
            "INVALIDO" => (RespuestaNodo.Rechazado, TimeSpan.Zero),
            "RECHAZADO" => (RespuestaNodo.ClaveInvalida, TimeSpan.FromSeconds(60)),
            "LIMITE" => (RespuestaNodo.Esperar, TimeSpan.FromSeconds(10)),
            _ => (RespuestaNodo.SinConexion, TimeSpan.FromSeconds(2)),
        };
    }

    private async Task<Conexion> ConectadoAsync(NodoSimulado nodo, CancellationToken ct)
    {
        if (conexiones.TryGetValue(nodo.Mac, out var c) && c.Cliente.IsConnected) return c;
        if (c is not null) { c.Cliente.Dispose(); conexiones.TryRemove(nodo.Mac, out _); }

        var cliente = new MqttClientFactory().CreateMqttClient();
        var nueva = new Conexion(cliente);
        cliente.ApplicationMessageReceivedAsync += e =>
        {
            nueva.Respuestas.Enqueue(JsonSerializer.Deserialize<JsonElement>(e.ApplicationMessage.ConvertPayloadToString()));
            nueva.Llegadas.Release();
            return Task.CompletedTask;
        };
        var usuario = config.Usuario(nodo.Mac);
        var r = await cliente.ConnectAsync(new MqttClientOptionsBuilder()
            .WithTcpServer(config.Servidor, config.Puerto).WithProtocolVersion(MqttProtocolVersion.V311)
            .WithClientId(usuario).WithCredentials(usuario, nodo.Clave).WithCleanSession(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30)).WithTimeout(TimeSpan.FromSeconds(5)).Build(), ct);
        if (r.ResultCode != MqttClientConnectResultCode.Success)
        {
            cliente.Dispose();
            throw new ConexionRechazada();
        }
        await cliente.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter($"{config.Raiz(nodo.Mac)}/respuestas", MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);
        conexiones[nodo.Mac] = nueva;
        return nueva;
    }

    private sealed class ConexionRechazada : Exception;

    public async ValueTask DisposeAsync()
    {
        foreach (var c in conexiones.Values)
        {
            if (c.Cliente.IsConnected) await c.Cliente.DisconnectAsync();
            c.Cliente.Dispose();
        }
    }
}
