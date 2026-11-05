using FlowTwin.Atenciones.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Atenciones.Application;

/// <summary>Implementa IAtencionesEnCurso. Solo depende de la base, para no formar ciclos entre módulos.</summary>
public sealed class AtencionesEnCursoConsulta(IAtencionesDb db) : IAtencionesEnCurso
{
    public async Task<IReadOnlyList<AtencionEnCurso>> DeSedeAsync(int sedeId, CancellationToken ct = default)
    {
        var abiertas = await db.Atenciones.AsNoTracking()
            .Where(a => a.SedeId == sedeId && a.FinEn == null)
            .OrderBy(a => a.InicioEn)
            .Select(a => new { a.Id, a.Codigo, a.SedeId, a.InicioEn })
            .ToListAsync(ct);
        var ids = abiertas.Select(a => a.Id).ToList();
        var estadias = (await db.EventosZona.AsNoTracking()
                .Where(e => ids.Contains(e.AtencionId))
                .OrderBy(e => e.EntradaEn)
                .Select(e => new { e.AtencionId, Estadia = new EstadiaInfo(e.ZonaId, e.EntradaEn, e.SalidaEn) })
                .ToListAsync(ct))
            .ToLookup(e => e.AtencionId, e => e.Estadia);
        return [.. abiertas.Select(a => new AtencionEnCurso(a.Id, a.Codigo, a.SedeId, a.InicioEn, [.. estadias[a.Id]]))];
    }
}
