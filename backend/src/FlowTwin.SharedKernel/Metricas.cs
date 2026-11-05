using System.Diagnostics.Metrics;

namespace FlowTwin.SharedKernel;

/// <summary>
/// Métricas del sistema con System.Diagnostics.Metrics (estándar de .NET, sin dependencias). Sirven para
/// medir las metas del documento con números (por ejemplo, el gemelo actualizado en 5 s o menos).
/// Se ven en vivo con:  dotnet-counters monitor -n FlowTwin.Api --counters FlowTwin.Ingesta
/// </summary>
public static class Metricas
{
    public static readonly Meter Ingesta = new("FlowTwin.Ingesta", "1.0");

    public static readonly Counter<long> LotesRecibidos =
        Ingesta.CreateCounter<long>("flowtwin.ingesta.lotes", "{lote}", "Lotes de lecturas aceptados");

    public static readonly Counter<long> LecturasRecibidas =
        Ingesta.CreateCounter<long>("flowtwin.ingesta.lecturas", "{lectura}", "Lecturas BLE guardadas");

    public static readonly Counter<long> LotesDuplicados =
        Ingesta.CreateCounter<long>("flowtwin.ingesta.duplicados", "{lote}", "Lotes reenviados que ya se habían recibido");

    public static readonly Counter<long> Rechazos =
        Ingesta.CreateCounter<long>("flowtwin.ingesta.rechazos", "{peticion}", "Peticiones de nodos rechazadas (etiqueta: motivo)");

    public static readonly Counter<long> Latidos =
        Ingesta.CreateCounter<long>("flowtwin.ingesta.latidos", "{latido}", "Latidos de nodos recibidos");

    public static readonly Histogram<double> DuracionLote =
        Ingesta.CreateHistogram<double>("flowtwin.ingesta.duracion", "ms", "Tiempo en procesar un lote, de la llegada al 202");
}
