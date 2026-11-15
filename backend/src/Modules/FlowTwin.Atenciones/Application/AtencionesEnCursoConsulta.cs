using FlowTwin.SharedKernel;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Atenciones.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Atenciones.Application;

/// <summary>Implementa IAtencionesEnCurso. Solo depende de la base, para no formar ciclos entre módulos.</summary>
public sealed class AtencionesEnCursoConsulta(IAtencionesDb db) : IAtencionesEnCurso, IAtencionesCerradas
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

    /// <summary>Tope de seguridad: un día normal del piloto tiene decenas, no cientos.</summary>
    private const int MaximoDelDia = 300;

    public async Task<IReadOnlyList<AtencionCerrada>> DelDiaAsync(int sedeId, DateOnly fecha, CancellationToken ct = default)
    {
        var cerradas = await db.Atenciones.AsNoTracking()
            .Where(a => a.SedeId == sedeId && a.FechaAtencion == fecha && a.FinEn != null && a.MotivoCierreId != MotivoCierreAtencion.Anulada)
            .OrderByDescending(a => a.FinEn).Take(MaximoDelDia)
            .Select(a => new { a.Id, a.Codigo, a.SedeId, a.InicioEn, FinEn = a.FinEn!.Value, Motivo = a.MotivoCierreId!.Value })
            .ToListAsync(ct);
        var ids = cerradas.Select(a => a.Id).ToList();
        var estadias = (await db.EventosZona.AsNoTracking()
                .Where(e => ids.Contains(e.AtencionId))
                .OrderBy(e => e.EntradaEn)
                .Select(e => new { e.AtencionId, Estadia = new EstadiaInfo(e.ZonaId, e.EntradaEn, e.SalidaEn) })
                .ToListAsync(ct))
            .ToLookup(e => e.AtencionId, e => e.Estadia);
        return [.. cerradas.Select(a => new AtencionCerrada(a.Id, a.Codigo, a.SedeId, a.InicioEn, a.FinEn, a.Motivo.Codigo(), [.. estadias[a.Id]]))];
    }
}
