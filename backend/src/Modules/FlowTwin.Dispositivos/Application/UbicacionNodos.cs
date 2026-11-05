using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Dispositivos.Application;

/// <summary>
/// Implementa IUbicacionNodos, el contrato que Gemelo declara para dibujar los nodos en el plano.
/// Solo depende de la base, no de otros servicios, para no formar un ciclo con Gemelo.
/// </summary>
public sealed class UbicacionNodos(IDispositivosDb db) : IUbicacionNodos
{
    /// <summary>Nodos con posición conocida de esas zonas, sin los dados de baja.</summary>
    public async Task<IReadOnlyList<NodoEnPlano>> DeZonasAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default)
    {
        var lista = await db.Nodos
            .Where(n => zonaIds.Contains(n.ZonaId) && n.EstadoNodoId != EstadoNodo.Baja && n.PosX != null && n.PosY != null)
            .OrderBy(n => n.Codigo)
            .ToListAsync(ct);
        return [.. lista.Select(n => new NodoEnPlano(n.Codigo, n.PosX!.Value, n.PosY!.Value, n.ZonaId, n.EstadoNodoId.Codigo()))];
    }
}
