using System.Buffers;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowTwin.Mensajeria.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace FlowTwin.Mensajeria.Application;

/// <summary>
/// El backend como suscriptor (ADR-24): se conecta al broker con su propia cuenta y sesión persistente
/// (si la API se reinicia, el broker guarda los mensajes QoS 1 y los entrega al volver), recibe lecturas y
/// latidos de todos los nodos, se los pasa al manejador de cada tipo y le contesta al nodo en su tema
/// «respuestas». El mensaje se confirma al broker DESPUÉS de procesarlo: si el proceso se cae a la mitad,
/// el broker lo vuelve a entregar, y la secuencia del lote evita guardarlo dos veces.
/// </summary>
public sealed class ServicioIngestaMqtt(
    IServiceScopeFactory scopes, AdministracionBroker administracion, IOptions<OpcionesMqtt> opciones,
    IReloj reloj, ILogger<ServicioIngestaMqtt> log) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Meter Medidor = new("FlowTwin.Mensajeria");
    private static readonly Counter<long> Recibidos = Medidor.CreateCounter<long>("flowtwin.mqtt.mensajes", description: "Mensajes de nodos procesados, por tipo y estado");

    private readonly OpcionesMqtt o = opciones.Value;
    private readonly TemasMqtt temas = new(opciones.Value.Prefijo);
    private readonly LimitePorNodo limite = new(reloj, opciones.Value.MensajesPorMinutoPorNodo);
    private readonly TaskCompletionSource conectado = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Se completa con la primera conexión. Lo usan las pruebas para no publicar antes de que haya quien reciba.</summary>
    public Task Conectado => conectado.Task;

    private volatile bool escuchando;

    /// <summary>True mientras está conectado al broker y suscrito: lo informa /salud.</summary>
    public bool Escuchando => escuchando;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!o.Activo)
        {
            log.LogWarning("MQTT apagado (Mqtt:Activo = false): no se reciben lecturas ni latidos de los nodos");
            return;
        }
        var tipos = TiposRegistrados();
        var espera = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            using var cliente = new MqttClientFactory().CreateMqttClient();
            var desconectado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cliente.DisconnectedAsync += _ => { desconectado.TrySetResult(); return Task.CompletedTask; };
            cliente.ApplicationMessageReceivedAsync += e => ProcesarAsync(cliente, e, ct);
            try
            {
                var clave = await administracion.PrepararIngestaAsync(ct);
                await ClienteMqtt.ConectarAsync(cliente, ClienteMqtt.Opciones(o, temas.UsuarioIngesta, temas.UsuarioIngesta, clave, limpia: false), ct);
                var suscripcion = new MqttClientSubscribeOptionsBuilder();
                foreach (var tipo in tipos) suscripcion.WithTopicFilter(temas.Filtro(tipo), MqttQualityOfServiceLevel.AtLeastOnce);
                await cliente.SubscribeAsync(suscripcion.Build(), ct);
                log.LogInformation("Conectado al broker MQTT {Servidor}:{Puerto}; escuchando {Temas}", o.Servidor, o.Puerto, string.Join(", ", tipos.Select(temas.Filtro)));
                conectado.TrySetResult();
                escuchando = true;
                espera = TimeSpan.FromSeconds(1);
                await desconectado.Task.WaitAsync(ct);
                escuchando = false;
                log.LogWarning("Se perdió la conexión con el broker MQTT; reintentando");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                escuchando = false;
                log.LogError("No se pudo conectar al broker MQTT {Servidor}:{Puerto} ({Error}); reintento en {Espera} s",
                    o.Servidor, o.Puerto, e.Message, espera.TotalSeconds);
                try { await Task.Delay(espera, ct); } catch (OperationCanceledException) { break; }
                espera = TimeSpan.FromSeconds(Math.Min(espera.TotalSeconds * 2, o.ReintentoMaximoSegundos));
            }
            if (cliente.IsConnected) await cliente.DisconnectAsync(cancellationToken: CancellationToken.None);
        }
    }

    private async Task ProcesarAsync(IMqttClient cliente, MqttApplicationMessageReceivedEventArgs e, CancellationToken ct)
    {
        if (temas.Leer(e.ApplicationMessage.Topic) is not var (mac, tipo)) return;   // el rol del broker no debería permitirlo

        RespuestaNodo respuesta;
        if (!limite.Permitir(mac))
            respuesta = new RespuestaNodo(tipo, RespuestaNodo.Limite);
        else
        {
            try
            {
                using var scope = scopes.CreateScope();
                var manejador = scope.ServiceProvider.GetServices<IManejadorNodo>().SingleOrDefault(m => m.Tipo == tipo);
                if (manejador is null) return;
                respuesta = await manejador.ManejarAsync(new MensajeNodo(mac, e.ApplicationMessage.Payload.ToArray()), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogError(ex, "Falló el procesamiento de {Tipo} del nodo {Mac}", tipo, mac);
                respuesta = new RespuestaNodo(tipo, RespuestaNodo.Error);
            }
        }
        Recibidos.Add(1, new KeyValuePair<string, object?>("tipo", tipo), new KeyValuePair<string, object?>("estado", respuesta.Estado));

        try
        {
            await cliente.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(temas.Tema(mac, TemasMqtt.Respuestas))
                .WithPayload(JsonSerializer.SerializeToUtf8Bytes(respuesta, Json))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Sin respuesta, el nodo reintenta el mismo lote y la secuencia lo marca como duplicado: no se pierde nada
            log.LogWarning(ex, "No se pudo responder al nodo {Mac}", mac);
        }
    }

    private string[] TiposRegistrados()
    {
        using var scope = scopes.CreateScope();
        return [.. scope.ServiceProvider.GetServices<IManejadorNodo>().Select(m => m.Tipo).Distinct()];
    }
}
