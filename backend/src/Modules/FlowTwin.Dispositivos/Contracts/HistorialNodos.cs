namespace FlowTwin.Dispositivos.Contracts;

/// <summary>Un cambio de estado de un nodo (dis.EventoNodo): desde «En», el nodo estaba (o no) En línea.</summary>
public sealed record CambioNodo(int NodoId, int ZonaId, bool EnLinea, DateTime En);

/// <summary>
/// Lo que Reportes necesita para no confundir «no había nadie» con «no sabemos»: en qué momentos cada
/// zona tuvo al menos un nodo En línea. Sale del historial de estados de los nodos, que ya se guarda.
/// </summary>
public interface IHistorialNodos
{
    /// <summary>
    /// Para cada nodo de esas zonas, incluidos los dados de baja (pudieron cubrir la zona en ese periodo):
    /// el estado que tenía al empezar el periodo (si ya existía) y cada cambio dentro de él, en orden.
    /// Usa la zona actual del nodo.
    /// </summary>
    Task<IReadOnlyList<CambioNodo>> DeZonasAsync(IReadOnlyCollection<int> zonaIds, DateTime desdeUtc, DateTime hastaUtc, CancellationToken ct = default);
}
