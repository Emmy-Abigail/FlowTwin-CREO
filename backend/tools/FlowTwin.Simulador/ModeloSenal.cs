namespace FlowTwin.Simulador;

/// <summary>
/// Señal BLE según la distancia, con el modelo de propagación "log-distance", el estándar para BLE en
/// interiores:  RSSI = P₀ − 10 · n · log₁₀(d) + ruido
/// P₀ es la señal a 1 m (el "measured power" del iBeacon) y n el exponente de pérdida: 2 en espacio
/// abierto, 2–3 en interiores. No modela las paredes: el motor debe acertar igual, y el piloto con las
/// tarjetas reales dirá cuánto ajustar (AjusteRssiDb).
/// </summary>
public static class ModeloSenal
{
    public const double SenalA1Metro = -59;
    public const double ExponentePerdida = 2.2;
    /// <summary>Más débil que esto, el ESP32 no detecta el anuncio.</summary>
    public const int Sensibilidad = -95;

    public static double Distancia((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    /// <summary>RSSI entero, como lo informa el ESP32. A menos de 0,5 m se toma 0,5 m (el log se dispara).</summary>
    public static int Rssi(double distanciaM, double ruidoDb, Random azar)
    {
        var d = Math.Max(distanciaM, 0.5);
        var rssi = SenalA1Metro - 10 * ExponentePerdida * Math.Log10(d) + Gauss(azar) * ruidoDb;
        return (int)Math.Round(Math.Clamp(rssi, -127, 0));
    }

    /// <summary>Ruido con forma de campana (normal estándar), por el método de Box–Muller.</summary>
    private static double Gauss(Random azar) =>
        Math.Sqrt(-2 * Math.Log(1 - azar.NextDouble())) * Math.Cos(2 * Math.PI * azar.NextDouble());
}
