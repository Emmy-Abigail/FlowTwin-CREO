namespace FlowTwin.Atenciones.Contracts;

/// <summary>Una estadía en una zona (ope.EventoZona). SalidaEn nula: está ahí ahora.</summary>
public sealed record EstadiaInfo(int ZonaId, DateTime EntradaEn, DateTime? SalidaEn);

/// <summary>Una atención abierta (tarjeta En uso) con su recorrido hasta ahora.</summary>
public sealed record AtencionEnCurso(long Id, string Codigo, int SedeId, DateTime InicioEn, IReadOnlyList<EstadiaInfo> Estadias);

/// <summary>Lo que Monitoreo necesita de Atenciones para el estado del piso (F-11).</summary>
public interface IAtencionesEnCurso
{
    /// <summary>Atenciones abiertas de la sede, con sus estadías en orden.</summary>
    Task<IReadOnlyList<AtencionEnCurso>> DeSedeAsync(int sedeId, CancellationToken ct = default);
}

/// <summary>Una atención terminada (no anulada) con su recorrido. Motivo: DEVUELTA, SIN_LECTURAS o CIERRE_JORNADA.</summary>
public sealed record AtencionCerrada(
    long Id, string Codigo, int SedeId, DateTime InicioEn, DateTime FinEn, string Motivo, IReadOnlyList<EstadiaInfo> Estadias);

/// <summary>Lo que Monitoreo necesita para «Atendidos hoy» del gemelo.</summary>
public interface IAtencionesCerradas
{
    /// <summary>Las atenciones de la sede cerradas en esa fecha, sin las anuladas (RN-04), de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<AtencionCerrada>> DelDiaAsync(int sedeId, DateOnly fecha, CancellationToken ct = default);
}
