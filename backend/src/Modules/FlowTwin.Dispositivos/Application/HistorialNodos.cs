using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Dispositivos.Application;

/// <summary>Implementa IHistorialNodos. Solo depende de la base, como PresenciaDispositivos.</summary>
public sealed class HistorialNodos(IDispositivosDb db) : IHistorialNodos
{
    public async Task<IReadOnlyList<CambioNodo>> DeZonasAsync(IReadOnlyCollection<int> zonaIds, DateTime desdeUtc, DateTime hastaUtc, CancellationToken ct = default)
    {
        var nodos = await db.Nodos.AsNoTracking().IgnoreQueryFilters()
            .Where(n => zonaIds.Contains(n.ZonaId))
            .Select(n => new { n.Id, n.ZonaId })
            .ToListAsync(ct);
        var cambios = new List<CambioNodo>();
        // Pocos nodos por zona: una consulta corta por nodo es más clara que un agrupamiento en SQL
        foreach (var n in nodos)
        {
            var alEmpezar = await db.EventosNodo.AsNoTracking()
                .Where(e => e.NodoId == n.Id && e.FechaRegistro <= desdeUtc)
                .OrderByDescending(e => e.FechaRegistro).ThenByDescending(e => e.Id)
                .Select(e => new { e.EstadoNodoId, e.FechaRegistro })
                .FirstOrDefaultAsync(ct);
            if (alEmpezar is not null)
                cambios.Add(new CambioNodo(n.Id, n.ZonaId, alEmpezar.EstadoNodoId == EstadoNodo.EnLinea, alEmpezar.FechaRegistro));
            cambios.AddRange((await db.EventosNodo.AsNoTracking()
                    .Where(e => e.NodoId == n.Id && e.FechaRegistro > desdeUtc && e.FechaRegistro < hastaUtc)
                    .OrderBy(e => e.FechaRegistro).ThenBy(e => e.Id)
                    .Select(e => new { e.EstadoNodoId, e.FechaRegistro })
                    .ToListAsync(ct))
                .Select(e => new CambioNodo(n.Id, n.ZonaId, e.EstadoNodoId == EstadoNodo.EnLinea, e.FechaRegistro)));
        }
        return cambios;
    }
}
