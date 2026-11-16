namespace FlowTwin.Atenciones.Contracts;

/// <summary>Una estadía en una zona (ope.EventoZona). SalidaEn nula: está ahí ahora.</summary>
public sealed record EstadiaInfo(int ZonaId, DateTime EntradaEn, DateTime? SalidaEn);

/// <summary>
/// Una atención abierta (tarjeta En uso) con su recorrido hasta ahora. UltimaLecturaEn: la última vez que
/// un nodo la vio (se actualiza como mucho cada 30 s); nula si nunca se la detectó.
/// </summary>
public sealed record AtencionEnCurso(long Id, string Codigo, int SedeId, DateTime InicioEn, IReadOnlyList<EstadiaInfo> Estadias,
    DateTime? UltimaLecturaEn = null, int TarjetaId = 0);

/// <summary>Lo que Monitoreo necesita de Atenciones para el estado del piso (F-11).</summary>
public interface IAtencionesEnCurso
{
    /// <summary>Atenciones abiertas de la sede, con sus estadías en orden.</summary>
    Task<IReadOnlyList<AtencionEnCurso>> DeSedeAsync(int sedeId, CancellationToken ct = default);
}

/// <summary>Una atención terminada (no anulada) con su recorrido. Motivo: DEVUELTA, SIN_LECTURAS o CIERRE_JORNADA.</summary>
public sealed record AtencionCerrada(
    long Id, string Codigo, int SedeId, DateTime InicioEn, DateTime FinEn, string Motivo, IReadOnlyList<EstadiaInfo> Estadias);

/// <summary>
/// Una atención cualquiera (abierta, cerrada o anulada) con todo su recorrido, para su detalle (P6).
/// Motivo: nulo si sigue abierta; si no, DEVUELTA, SIN_LECTURAS, CIERRE_JORNADA o ANULADA.
/// </summary>
public sealed record AtencionDetalleInfo(
    long Id, string Codigo, DateOnly Fecha, int SedeId, int TarjetaId, DateTime InicioEn, DateTime? FinEn, string? Motivo,
    IReadOnlyList<EstadiaInfo> Estadias);

/// <summary>Lo que el módulo Reportes necesita: el detalle de una atención (P6) y las de un periodo (P5).</summary>
public interface IAtencionesConsulta
{
    /// <summary>La atención con ese Id y sus estadías en orden, o null si no existe. No revisa permisos.</summary>
    Task<AtencionDetalleInfo?> PorIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Las atenciones de la sede con fecha (día de Lima) entre «desde» y «hasta», ambos incluidos, abiertas
    /// o cerradas, SIN las anuladas (RN-04), con sus estadías en orden. No revisa permisos.
    /// </summary>
    Task<IReadOnlyList<AtencionDetalleInfo>> DelPeriodoAsync(int sedeId, DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

/// <summary>Lo que Monitoreo necesita para «Atendidos hoy» del gemelo.</summary>
public interface IAtencionesCerradas
{
    /// <summary>Las atenciones de la sede cerradas en esa fecha, sin las anuladas (RN-04), de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<AtencionCerrada>> DelDiaAsync(int sedeId, DateOnly fecha, CancellationToken ct = default);
}
