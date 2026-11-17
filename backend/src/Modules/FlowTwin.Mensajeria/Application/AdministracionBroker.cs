using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTwin.Mensajeria.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Exceptions;
using MQTTnet.Protocol;

namespace FlowTwin.Mensajeria.Application;

/// <summary>
/// Administra cuentas y permisos del broker con el plugin Dynamic Security de Mosquitto: publica comandos
/// JSON en $CONTROL/dynamic-security/v1 y espera la respuesta en …/response. Una sola conexión, reutilizada.
/// Cada nodo tiene su cuenta y su rol: solo puede publicar en SUS temas y leer SUS respuestas.
/// </summary>
public sealed class AdministracionBroker(IOptions<OpcionesMqtt> opciones, ILogger<AdministracionBroker> log)
    : ICuentasNodosBroker, IAsyncDisposable
{
    private const string TemaControl = "$CONTROL/dynamic-security/v1";
    private const string TemaRespuesta = TemaControl + "/response";
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);

    private readonly OpcionesMqtt o = opciones.Value;
    private readonly TemasMqtt temas = new(opciones.Value.Prefijo);
    private readonly SemaphoreSlim conexion = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> pendientes = new();
    private IMqttClient? cliente;

    public CuentaMqtt CuentaDe(string mac) => temas.CuentaDe(mac);

    public async Task<CuentaMqtt> CrearAsync(string mac, string clave, CancellationToken ct = default)
    {
        if (!o.Activo) { log.LogWarning("MQTT apagado: la cuenta de {Mac} no se creó en el broker", mac); return temas.CuentaDe(mac); }
        var usuario = temas.UsuarioNodo(mac);
        // Borrar y crear: si existía, el broker cierra la sesión abierta con la clave anterior
        await EjecutarAsync([
            Comando("deleteClient", ("username", usuario)),
            Comando("deleteRole", ("rolename", usuario)),
            Comando("createRole", ("rolename", usuario), ("acls", Acls(
                ("publishClientSend", temas.Tema(mac, TemasMqtt.Lecturas)),
                ("publishClientSend", temas.Tema(mac, TemasMqtt.Latidos)),
                ("subscribeLiteral", temas.Tema(mac, TemasMqtt.Respuestas))))),
            Comando("createClient", ("username", usuario), ("password", clave), ("roles", Roles(usuario))),
        ], ignorarNoExiste: true, ct);
        log.LogInformation("Cuenta MQTT {Usuario} creada", usuario);
        return temas.CuentaDe(mac);
    }

    public async Task EliminarAsync(string mac, CancellationToken ct = default)
    {
        if (!o.Activo) return;
        var usuario = temas.UsuarioNodo(mac);
        await EjecutarAsync([Comando("deleteClient", ("username", usuario)), Comando("deleteRole", ("rolename", usuario))], ignorarNoExiste: true, ct);
        log.LogInformation("Cuenta MQTT {Usuario} eliminada", usuario);
    }

    public async Task<IReadOnlyList<string>> MacsConCuentaAsync(CancellationToken ct = default)
    {
        if (!o.Activo) return [];
        var r = await EjecutarAsync([Comando("listClients", ("verbose", false), ("count", -1), ("offset", 0))], ignorarNoExiste: false, ct);
        return [.. (r[0]["data"]?["clients"]?.AsArray() ?? [])
            .Select(c => c?.GetValue<string>())
            .Select(u => u is null ? null : temas.MacDeUsuario(u))
            .OfType<string>()];
    }

    /// <summary>
    /// Al arrancar: todo cerrado por defecto (nadie publica ni se suscribe si su rol no lo permite) y la
    /// cuenta del cliente de ingesta, con una clave aleatoria nueva que solo conoce este proceso.
    /// </summary>
    public async Task<string> PrepararIngestaAsync(CancellationToken ct)
    {
        var clave = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var usuario = temas.UsuarioIngesta;
        await EjecutarAsync([
            Comando("setDefaultACLAccess", ("acls", new JsonArray(
                Acceso("publishClientSend", false), Acceso("publishClientReceive", true),
                Acceso("subscribe", false), Acceso("unsubscribe", true)))),
            Comando("deleteClient", ("username", usuario)),
            Comando("deleteRole", ("rolename", usuario)),
            Comando("createRole", ("rolename", usuario), ("acls", Acls(
                ("subscribePattern", temas.Filtro(TemasMqtt.Lecturas)),
                ("subscribePattern", temas.Filtro(TemasMqtt.Latidos)),
                ("publishClientSend", temas.Filtro(TemasMqtt.Respuestas))))),
            Comando("createClient", ("username", usuario), ("password", clave), ("roles", Roles(usuario))),
        ], ignorarNoExiste: true, ct);
        return clave;
    }

    /// <summary>Envía los comandos en un solo mensaje y espera sus respuestas. 503 si el broker no responde.</summary>
    private async Task<List<JsonObject>> EjecutarAsync(IReadOnlyList<JsonObject> comandos, bool ignorarNoExiste, CancellationToken ct)
    {
        var lote = Guid.NewGuid().ToString("N");
        var esperas = comandos.Select((c, i) =>
        {
            var id = $"{lote}-{i}";
            c["correlationData"] = id;
            var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            pendientes[id] = tcs;
            return (id, tcs.Task);
        }).ToList();
        try
        {
            var c = await ConectadoAsync(ct);
            var cuerpo = new JsonObject { ["commands"] = new JsonArray([.. comandos]) }.ToJsonString();
            await c.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(TemaControl).WithPayload(cuerpo)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);
            var respuestas = await Task.WhenAll(esperas.Select(e => e.Task)).WaitAsync(Espera, ct);
            foreach (var r in respuestas)
            {
                var error = r["error"]?.GetValue<string>();
                if (error is null || (ignorarNoExiste && error.Contains("not found", StringComparison.OrdinalIgnoreCase))) continue;
                throw new InvalidOperationException($"El broker rechazó «{r["command"]}»: {error}");
            }
            return [.. respuestas];
        }
        catch (Exception e) when (e is TimeoutException or MqttCommunicationException || (e is OperationCanceledException && !ct.IsCancellationRequested))
        {
            log.LogError(e, "El broker MQTT no respondió a la administración de cuentas");
            throw new ServicioNoDisponibleException("El broker MQTT no responde. Revisa que Mosquitto esté encendido y vuelve a intentarlo.");
        }
        finally
        {
            foreach (var (id, _) in esperas) pendientes.TryRemove(id, out _);
        }
    }

    private async Task<IMqttClient> ConectadoAsync(CancellationToken ct)
    {
        await conexion.WaitAsync(ct);
        try
        {
            if (cliente?.IsConnected == true) return cliente;
            if (string.IsNullOrWhiteSpace(o.ClaveAdmin))
                throw new InvalidOperationException("Falta Mqtt:ClaveAdmin. Guárdala con: dotnet user-secrets set \"Mqtt:ClaveAdmin\" \"...\"");
            cliente?.Dispose();
            cliente = new MqttClientFactory().CreateMqttClient();
            cliente.ApplicationMessageReceivedAsync += e =>
            {
                RecibirRespuestas(e.ApplicationMessage.ConvertPayloadToString());
                return Task.CompletedTask;
            };
            await ClienteMqtt.ConectarAsync(cliente, ClienteMqtt.Opciones(o, $"{temas.Prefijo}-admin-{Environment.ProcessId}", o.UsuarioAdmin, o.ClaveAdmin, limpia: true), ct);
            await cliente.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(TemaRespuesta, MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);
            return cliente;
        }
        finally { conexion.Release(); }
    }

    private void RecibirRespuestas(string json)
    {
        try
        {
            foreach (var r in JsonNode.Parse(json)?["responses"]?.AsArray() ?? [])
                if (r is JsonObject obj && obj["correlationData"]?.GetValue<string>() is { } id && pendientes.TryGetValue(id, out var tcs))
                    tcs.TrySetResult(obj);
        }
        catch (JsonException e) { log.LogWarning(e, "Respuesta del broker que no es JSON"); }
    }

    private static JsonObject Comando(string nombre, params (string Clave, object Valor)[] campos)
    {
        var c = new JsonObject { ["command"] = nombre };
        foreach (var (k, v) in campos) c[k] = v is JsonNode n ? n : JsonValue.Create(v);
        return c;
    }

    private static JsonArray Acls(params (string Tipo, string Tema)[] acls) =>
        new([.. acls.Select(a => (JsonNode)new JsonObject { ["acltype"] = a.Tipo, ["topic"] = a.Tema, ["priority"] = 0, ["allow"] = true })]);

    private static JsonObject Acceso(string tipo, bool permitir) => new() { ["acltype"] = tipo, ["allow"] = permitir };

    private static JsonArray Roles(string rol) => new(new JsonObject { ["rolename"] = rol, ["priority"] = 0 });

    public async ValueTask DisposeAsync()
    {
        if (cliente is { IsConnected: true }) await cliente.DisconnectAsync();
        cliente?.Dispose();
        conexion.Dispose();
    }
}
