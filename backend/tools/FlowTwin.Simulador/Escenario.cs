using System.Text.Json;

namespace FlowTwin.Simulador;

/// <summary>Un intervalo de minutos; cada paciente toma un valor al azar dentro de él.</summary>
public sealed record Rango(double Min, double Max)
{
    public TimeSpan Elegir(Random azar) => TimeSpan.FromMinutes(Min + azar.NextDouble() * (Max - Min));
}

/// <summary>Un ESP32 receptor simulado: su código, su MAC y su clave (las de la semilla de desarrollo).</summary>
public sealed record NodoSimulado(string Codigo, string Mac, string Clave);

/// <summary>
/// El broker MQTT y el prefijo de temas y cuentas (contrato v2.0). El prefijo separa entornos que comparten
/// broker: el simulador usa «flowtwin-sim», el de la API del perfil «simulador».
/// </summary>
public sealed record ConexionMqtt(string Servidor, int Puerto, string Prefijo)
{
    /// <summary>El prefijo de la API de trabajo: el simulador no publica ahí salvo que se pida a propósito.</summary>
    public const string PrefijoDeTrabajo = "flowtwin";

    private static string Segmento(string mac) => mac.Replace(":", "").ToLowerInvariant();
    public string Usuario(string mac) => $"{Prefijo}-{Segmento(mac)}";
    public string Raiz(string mac) => $"{Prefijo}/v1/nodos/{Segmento(mac)}";
}

/// <summary>
/// Escenario de simulación, leído de escenarios/*.json. Los tiempos están en minutos para que una demo
/// rápida sea solo otro archivo, sin tocar código.
/// </summary>
public sealed record Escenario(
    string Api,
    string CorreoOperador,
    string ClaveOperador,
    int PisoId,
    string UuidClinica,
    int Sede,
    Rango LlegadaCada,
    Rango EnAdmision,
    Rango Espera,
    Rango Consulta,
    int ZonaAdmision,
    int ZonaEspera,
    int ZonaConsulta,
    int MaxPacientes,
    double RuidoDb,
    int? Semilla,
    ConexionMqtt Mqtt,
    List<NodoSimulado> Nodos)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip };

    /// <summary>
    /// La API de trabajo (perfil «http», puerto 5080) usa la base donde se registra a mano. El simulador
    /// escribe atenciones y lecturas como si fueran reales, así que va a la API del perfil «simulador»
    /// (puerto 5090, base FlowTwin_Simulador). Apuntar a la de trabajo exige pedirlo a propósito.
    /// </summary>
    public const int PuertoApiDeTrabajo = 5080;

    public bool ApuntaALaApiDeTrabajo =>
        (Uri.TryCreate(Api, UriKind.Absolute, out var u) && u.IsLoopback && u.Port == PuertoApiDeTrabajo)
        || Mqtt.Prefijo == ConexionMqtt.PrefijoDeTrabajo;

    public static Escenario Leer(string ruta) =>
        JsonSerializer.Deserialize<Escenario>(File.ReadAllText(ruta), Json)
        ?? throw new InvalidOperationException($"El escenario {ruta} está vacío.");

    /// <summary>Identificador del emisor según el contrato: {uuid}-{major}-{minor}, sin ceros a la izquierda.</summary>
    public string UuidBle(int numeroTarjeta) => $"{UuidClinica}-{Sede}-{numeroTarjeta}";
}
