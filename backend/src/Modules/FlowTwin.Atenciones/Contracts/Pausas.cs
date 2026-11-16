namespace FlowTwin.Atenciones.Contracts;

/// <summary>Un periodo fuera de servicio de una zona (ADR-25). FinEn nulo: sigue fuera de servicio. Motivo: código del catálogo.</summary>
public sealed record PausaInfo(int ZonaId, string Motivo, string? Detalle, DateTime InicioEn, DateTime? FinEn, int UsuarioId);

/// <summary>Lo que el gemelo (Monitoreo) y el historial (Reportes) necesitan saber de las zonas fuera de servicio.</summary>
public interface IPausasZonas
{
    /// <summary>Las pausas abiertas de esas zonas (como mucho una por zona).</summary>
    Task<IReadOnlyList<PausaInfo>> AbiertasAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default);

    /// <summary>Las pausas de esas zonas que se cruzan con [desde, hasta).</summary>
    Task<IReadOnlyList<PausaInfo>> DelPeriodoAsync(IReadOnlyCollection<int> zonaIds, DateTime desde, DateTime hasta, CancellationToken ct = default);
}
