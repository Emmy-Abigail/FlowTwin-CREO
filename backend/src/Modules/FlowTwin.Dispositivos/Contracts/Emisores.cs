namespace FlowTwin.Dispositivos.Contracts;

/// <summary>
/// Identidad de los emisores BLE (ESP32 en modo iBeacon) según docs/contrato-integracion-nodos.md.
/// Un iBeacon anuncia UUID + major + minor: el UUID es el de la clínica (los nodos ignoran cualquier
/// otro), major es la sede y minor el número del emisor. El identificador que viaja en la ingesta es
/// "{uuid}-{major}-{minor}" en minúsculas y sin ceros a la izquierda: "…-1-7" para T-07 de la sede 1.
/// </summary>
public static class EmisorBle
{
    /// <summary>
    /// UUID propio de CREO+, generado al azar (v4). NO usar el UUID de fábrica de una marca de beacons:
    /// cualquier beacon de esa marca cerca de la clínica se confundiría con un paciente.
    /// </summary>
    public const string UuidClinica = "b14c70f2-314c-41eb-b11a-93fa9c7ace90";

    public static string Identificador(int major, int minor) => $"{UuidClinica}-{major}-{minor}";
}
