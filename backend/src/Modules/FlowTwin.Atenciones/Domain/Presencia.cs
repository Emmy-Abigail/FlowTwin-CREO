using FlowTwin.SharedKernel;

namespace FlowTwin.Atenciones.Domain;

/// <summary>
/// ope.EventoZona: una estadía de una atención en una zona. Inmutable salvo llenar SalidaEn una vez.
/// Una atención tiene como máximo un evento abierto (RN-11, garantizado también en la base).
/// </summary>
public class EventoZona : EntidadConActualizacion
{
    public long Id { get; set; }
    public long AtencionId { get; set; }
    public int ZonaId { get; set; }
    public DateTime EntradaEn { get; set; }
    public DateTime? SalidaEn { get; set; }

    /// <summary>Cierra la estadía. Nunca antes de la entrada (CHECK SalidaEn >= EntradaEn).</summary>
    public void Cerrar(DateTime en)
    {
        if (SalidaEn is not null) return;
        SalidaEn = en < EntradaEn ? EntradaEn : en;
    }
}

/// <summary>Parámetros del motor (sección "Presencia" de appsettings; los ajusta el equipo técnico).</summary>
public sealed record ParametrosPresencia(int RssiMinimo, TimeSpan Ventana, TimeSpan Histeresis, TimeSpan SinLecturaSalida)
{
    public static readonly ParametrosPresencia PorDefecto =
        new(-80, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(120));
}

/// <summary>Lo que el motor decide. El procesador lo convierte en filas de ope.EventoZona.</summary>
public abstract record DecisionPresencia(long AtencionId, DateTime En);

/// <summary>Entró a ZonaId en el instante En (cuando la zona empezó a ser la mejor, RN-10).</summary>
public sealed record EntradaZona(long AtencionId, int ZonaId, DateTime En) : DecisionPresencia(AtencionId, En);

/// <summary>Salió de la zona en que estaba, en el instante En.</summary>
public sealed record SalidaZona(long AtencionId, DateTime En) : DecisionPresencia(AtencionId, En);

/// <summary>
/// Motor de presencia (sección "Motor de presencia", RN-10 a RN-12). Es PURO: no conoce la base, el
/// reloj del sistema ni los hilos; recibe lecturas y devuelve decisiones. Así se prueba con cualquier
/// escenario en milisegundos y se puede ajustar en la calibración sin miedo.
///
/// Usa la hora de las lecturas (LeidaEn) y no la hora actual: reproducir lecturas viejas (al reiniciar,
/// o un lote que un nodo reenvió tras un corte) da exactamente las mismas decisiones.
/// </summary>
public sealed class MotorPresencia(ParametrosPresencia parametros)
{
    private readonly Dictionary<long, Rastreo> rastreos = [];

    public IReadOnlyCollection<long> AtencionesRastreadas => rastreos.Keys;

    /// <summary>Al arrancar (ADR-08): la atención ya está en esa zona según su evento abierto.</summary>
    public void Restaurar(long atencionId, int zonaId, DateTime desde) =>
        rastreos[atencionId] = new Rastreo { ZonaActual = zonaId, UltimaVezVista = desde };

    /// <summary>Deja de rastrear las atenciones que ya no están abiertas.</summary>
    public void Olvidar(IReadOnlySet<long> abiertas)
    {
        foreach (var id in rastreos.Keys.Where(id => !abiertas.Contains(id)).ToList()) rastreos.Remove(id);
    }

    /// <summary>
    /// Una lectura ya traducida: qué atención, en qué zona (la del nodo) y con qué señal, con el ajuste
    /// de calibración del nodo ya aplicado. Devuelve la entrada y salida si esta lectura confirma un cambio.
    /// </summary>
    public IReadOnlyList<DecisionPresencia> Registrar(long atencionId, int zonaId, int rssi, DateTime leidaEn)
    {
        if (!rastreos.TryGetValue(atencionId, out var r)) rastreos[atencionId] = r = new Rastreo();
        // Una lectura atrasada (un nodo que reenvía su búfer tras un corte de Wi-Fi) que ya quedó fuera de
        // la ventana no aporta al presente: se ignora para decidir. La lectura cruda sigue en ope.LecturaBle.
        if (r.UltimaVezVista != default && leidaEn < r.UltimaVezVista - parametros.Ventana) return [];
        r.Agregar(zonaId, rssi, leidaEn, parametros.Ventana);
        if (leidaEn > r.UltimaVezVista) r.UltimaVezVista = leidaEn;
        return Evaluar(atencionId, r, leidaEn);
    }

    /// <summary>
    /// Sin lecturas de ningún nodo por SinLecturaSalida: se cierra la estadía con la última vez que se la
    /// vio y la atención queda «sin zona» (por ejemplo, en el baño) hasta que reaparezca.
    /// </summary>
    public IReadOnlyList<DecisionPresencia> Revisar(DateTime ahora)
    {
        var decisiones = new List<DecisionPresencia>();
        foreach (var (id, r) in rastreos)
        {
            if (r.ZonaActual is null || ahora - r.UltimaVezVista < parametros.SinLecturaSalida) continue;
            decisiones.Add(new SalidaZona(id, r.UltimaVezVista));
            r.ZonaActual = null;
            r.Candidata = null;
        }
        return decisiones;
    }

    public int? ZonaDe(long atencionId) => rastreos.TryGetValue(atencionId, out var r) ? r.ZonaActual : null;

    private List<DecisionPresencia> Evaluar(long atencionId, Rastreo r, DateTime t)
    {
        var mejor = r.MejorZona(t, parametros);
        if (mejor is null || mejor == r.ZonaActual)
        {
            r.Candidata = null;   // se quedó donde estaba (o nadie supera el mínimo): se descarta el intento
            return [];
        }
        if (r.Candidata != mejor)
        {
            (r.Candidata, r.CandidataDesde) = (mejor, t);   // empieza a contar la histéresis
            return [];
        }
        if (t - r.CandidataDesde < parametros.Histeresis) return [];

        // Confirmado: la entrada se fecha cuando la zona EMPEZÓ a ser la mejor (RN-10)
        var desde = r.CandidataDesde;
        var decisiones = new List<DecisionPresencia>();
        if (r.ZonaActual is not null) decisiones.Add(new SalidaZona(atencionId, desde));
        decisiones.Add(new EntradaZona(atencionId, mejor.Value, desde));
        (r.ZonaActual, r.Candidata) = (mejor, null);
        return decisiones;
    }

    /// <summary>Estado de una atención: dónde está, qué zona intenta ganar y las señales recientes.</summary>
    private sealed class Rastreo
    {
        private readonly Dictionary<int, Queue<(DateTime En, int Rssi)>> senales = [];

        public int? ZonaActual { get; set; }
        public int? Candidata { get; set; }
        public DateTime CandidataDesde { get; set; }
        public DateTime UltimaVezVista { get; set; }

        public void Agregar(int zonaId, int rssi, DateTime en, TimeSpan ventana)
        {
            if (!senales.TryGetValue(zonaId, out var cola)) senales[zonaId] = cola = new();
            cola.Enqueue((en, rssi));
            // Las colas solo guardan la ventana: la memoria no crece con el tiempo
            foreach (var c in senales.Values)
                while (c.Count > 0 && en - c.Peek().En > ventana) c.Dequeue();
        }

        /// <summary>
        /// La zona con mejor promedio en la ventana que supere el mínimo. Si varios nodos ven la tarjeta,
        /// gana la señal sostenida, no la lectura suelta más fuerte.
        /// </summary>
        public int? MejorZona(DateTime t, ParametrosPresencia p)
        {
            int? mejor = null;
            var mejorPromedio = double.MinValue;
            foreach (var (zona, cola) in senales)
            {
                var recientes = cola.Where(s => s.En <= t && t - s.En <= p.Ventana).ToList();
                if (recientes.Count == 0) continue;
                var promedio = recientes.Average(s => s.Rssi);
                if (promedio < p.RssiMinimo || promedio <= mejorPromedio) continue;
                (mejor, mejorPromedio) = (zona, promedio);
            }
            return mejor;
        }
    }
}
