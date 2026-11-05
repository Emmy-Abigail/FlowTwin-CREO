namespace FlowTwin.Gemelo.Contracts;

/// <summary>Lo que otro módulo necesita saber de una zona.</summary>
public sealed record ZonaInfo(int Id, string Nombre, int PisoId);

/// <summary>Consultas de zonas para otros módulos (por ejemplo, Dispositivos al registrar un nodo, RN-19).</summary>
public interface IZonasConsultas
{
    /// <summary>La zona activa con ese Id, o null si no existe o está dada de baja.</summary>
    Task<ZonaInfo?> ZonaAsync(int zonaId, CancellationToken ct = default);

    Task<IReadOnlyList<ZonaInfo>> ZonasAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default);
}

/// <summary>Un nodo tal como se dibuja en el plano (sección "La geometría que recibe el navegador").</summary>
public sealed record NodoEnPlano(string Codigo, decimal X, decimal Y, int ZonaId, string Estado);

/// <summary>
/// Inversión de dependencias: Gemelo necesita los nodos para la geometría, pero no puede depender
/// de Dispositivos (Dispositivos ya depende de Gemelo y se formaría un ciclo). Por eso Gemelo
/// declara aquí lo que necesita y Dispositivos lo implementa.
/// </summary>
public interface IUbicacionNodos
{
    /// <summary>Nodos con posición conocida de esas zonas, sin los dados de baja.</summary>
    Task<IReadOnlyList<NodoEnPlano>> DeZonasAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default);
}
