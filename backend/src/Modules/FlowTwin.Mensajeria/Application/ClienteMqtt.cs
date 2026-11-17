using MQTTnet;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;

namespace FlowTwin.Mensajeria.Application;

/// <summary>Opciones de conexión comunes: MQTT 3.1.1 (el que soporta el firmware de los ESP32) y TLS en la clínica.</summary>
internal static class ClienteMqtt
{
    public static MqttClientOptions Opciones(OpcionesMqtt o, string clientId, string usuario, string clave, bool limpia)
    {
        var b = new MqttClientOptionsBuilder()
            .WithTcpServer(o.Servidor, o.Puerto)
            .WithClientId(clientId)
            .WithCredentials(usuario, clave)
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithCleanSession(limpia)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .WithTimeout(TimeSpan.FromSeconds(10));
        if (o.UsarTls) b = b.WithTlsOptions(t => t.UseTls());
        return b.Build();
    }

    /// <summary>
    /// Conecta y exige que el broker acepte: MQTTnet 5 NO lanza error con credenciales incorrectas, devuelve
    /// el código. Sin esta revisión, el backend creería estar conectado cuando el broker lo rechazó.
    /// </summary>
    public static async Task ConectarAsync(IMqttClient cliente, MqttClientOptions opciones, CancellationToken ct)
    {
        var r = await cliente.ConnectAsync(opciones, ct);
        if (r.ResultCode != MqttClientConnectResultCode.Success)
            throw new MqttCommunicationException($"El broker rechazó la conexión de {opciones.ClientId}: {r.ResultCode}");
    }
}
