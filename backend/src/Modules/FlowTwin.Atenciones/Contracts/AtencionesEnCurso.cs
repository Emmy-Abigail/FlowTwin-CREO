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
