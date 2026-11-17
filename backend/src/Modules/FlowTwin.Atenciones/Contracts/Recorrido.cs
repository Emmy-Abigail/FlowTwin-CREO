namespace FlowTwin.Atenciones.Contracts;

/// <summary>
/// Estado de una atención tal como se muestra. EN_ESPERA: aún no entra a una zona Servicio.
/// EN_ATENCION: está en una. ATENDIDO: ya pasó por una y salió, pero aún tiene la tarjeta.
/// FINALIZADA: se cerró (devolución, sin lecturas o cierre de jornada). ANULADA: la entrega se deshizo (RN-04).
/// </summary>
public static class EstadoAtencion
{
    public const string EnEspera = "EN_ESPERA", EnAtencion = "EN_ATENCION", Atendido = "ATENDIDO",
        Finalizada = "FINALIZADA", Anulada = "ANULADA";
}

/// <summary>
/// RN-06 en un solo lugar, para el gemelo (Monitoreo), el detalle de una atención y los reportes:
/// espera = desde la entrega hasta la PRIMERA entrada a una zona Servicio (si aún no entra, sigue
/// corriendo hasta «hasta»); consulta = tiempo total dentro de zonas Servicio. El triaje es TRIAJE, no
/// Servicio (ADR-26): es parte del camino hasta el médico, así que no detiene la espera («door-to-provider»). Es pura: recibe qué
/// zonas son Servicio, porque eso lo sabe el módulo Gemelo.
/// </summary>
public static class ReglaTiempos
{
    public const string Servicio = "SERVICIO";

    public static TiemposAtencion Calcular(IReadOnlyList<EstadiaInfo> estadias, DateTime inicio, DateTime hasta, Func<int, bool> esServicio)
    {
        var enServicio = estadias.Where(e => esServicio(e.ZonaId)).ToList();
        var primeraConsulta = enServicio.Select(e => (DateTime?)e.EntradaEn).Min();
        var espera = (primeraConsulta ?? hasta) - inicio;
        var consulta = enServicio.Aggregate(TimeSpan.Zero, (t, e) => t + ((e.SalidaEn ?? hasta) - e.EntradaEn));
        return new TiemposAtencion(espera, consulta, primeraConsulta);
    }

    /// <summary>
    /// Estado de una atención abierta según dónde está ahora. «Ahora» es su estadía sin salida, solo si
    /// cuenta: el gemelo de un piso no cuenta las estadías en zonas de otro piso.
    /// </summary>
    public static string EstadoAbierta(EstadiaInfo? actual, DateTime? primeraConsulta, Func<int, bool> esServicio) =>
        actual is not null && esServicio(actual.ZonaId) ? EstadoAtencion.EnAtencion
        : primeraConsulta is null ? EstadoAtencion.EnEspera
        : EstadoAtencion.Atendido;

    /// <summary>Minutos completos, nunca negativos: «12 min» recién se muestra al cumplirse el minuto 12.</summary>
    public static int Minutos(TimeSpan t) => (int)Math.Floor(Math.Max(0, t.TotalMinutes));
}

/// <summary>PrimeraConsulta nula: todavía no entró a una zona Servicio.</summary>
public sealed record TiemposAtencion(TimeSpan Espera, TimeSpan Consulta, DateTime? PrimeraConsulta);
