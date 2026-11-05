using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Dispositivos.Application;

/// <summary>Implementa IPresenciaDispositivos. Solo depende de la base, como UbicacionNodos, para no formar ciclos.</summary>
public sealed class PresenciaDispositivos(IDispositivosDb db) : IPresenciaDispositivos
{
    public async Task<IReadOnlyList<NodoZona>> NodosAsync(CancellationToken ct = default) =>
        await db.Nodos.Where(n => n.EstadoNodoId != EstadoNodo.Baja)
            .Select(n => new NodoZona(n.Id, n.ZonaId, n.AjusteRssiDb))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, string>> UuidsDeTarjetasAsync(IReadOnlyCollection<int> tarjetaIds, CancellationToken ct = default) =>
        await db.Tarjetas.Where(t => tarjetaIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.UuidBle.ToLowerInvariant(), ct);
}
