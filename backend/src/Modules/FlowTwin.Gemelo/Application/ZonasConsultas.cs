using FlowTwin.Gemelo.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Gemelo.Application;

/// <summary>
/// IZonasConsultas en su propia clase, separada de GemeloServicio a propósito: GemeloServicio usa
/// IUbicacionNodos (Dispositivos) y Dispositivos usa IZonasConsultas. Si un mismo servicio hiciera
/// ambas cosas, el contenedor de dependencias entraría en un ciclo al construirlos.
/// </summary>
public sealed class ZonasConsultas(IGemeloDb db) : IZonasConsultas
{
    public async Task<ZonaInfo?> ZonaAsync(int zonaId, CancellationToken ct = default) =>
        await db.Zonas.Where(z => z.Id == zonaId).Select(z => new ZonaInfo(z.Id, z.Nombre, z.PisoId)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ZonaInfo>> ZonasAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default) =>
        await db.Zonas.Where(z => zonaIds.Contains(z.Id)).Select(z => new ZonaInfo(z.Id, z.Nombre, z.PisoId)).ToListAsync(ct);
}
