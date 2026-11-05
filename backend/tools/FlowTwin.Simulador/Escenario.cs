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
    List<NodoSimulado> Nodos)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip };

    public static Escenario Leer(string ruta) =>
        JsonSerializer.Deserialize<Escenario>(File.ReadAllText(ruta), Json)
        ?? throw new InvalidOperationException($"El escenario {ruta} está vacío.");

    /// <summary>Identificador del emisor según el contrato: {uuid}-{major}-{minor}, sin ceros a la izquierda.</summary>
    public string UuidBle(int numeroTarjeta) => $"{UuidClinica}-{Sede}-{numeroTarjeta}";
}
