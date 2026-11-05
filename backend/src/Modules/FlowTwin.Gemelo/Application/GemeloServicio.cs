using FlowTwin.Gemelo.Contracts;
using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Gemelo.Application;

public interface IGemeloDb : IUnidadDeTrabajo
{
    DbSet<Sede> Sedes { get; }
    DbSet<Piso> Pisos { get; }
    DbSet<Zona> Zonas { get; }
}

public sealed record PisoDto(int Id, string Nombre, short Nivel, decimal? AnchoM, decimal? FondoM, bool Publicado);
public sealed record SedeDto(int Id, string Nombre, string? Direccion, IReadOnlyList<PisoDto> Pisos);

/// <summary>Una zona tal como la dibujan la vista 2D y la 3D (sección "La geometría que recibe el navegador").</summary>
public sealed record ZonaGeometriaDto(
    int Id, string Nombre, string Tipo, decimal X, decimal Y, decimal Ancho, decimal Fondo,
    short? Aforo, byte? UmbralMedia, byte? UmbralAlta);

/// <summary>
/// GET /pisos/{id}/geometria. Por ahora lleva el piso y sus zonas; "elementos" (muros) y "nodos"
/// se agregan cuando existan gem.ElementoPlano y dis.Nodo.
/// </summary>
public sealed record GeometriaPisoDto(
    int PisoId, string Nombre, short Nivel, decimal? AnchoM, decimal? FondoM, decimal AltoMuroM,
    string? PlanoUrl, IReadOnlyList<ZonaGeometriaDto> Zonas);

public sealed class GemeloServicio(IGemeloDb db, IUsuarioActual usuario) : ISedesConsultas
{
    /// <summary>GET /sedes: sedes y pisos publicados que el usuario puede ver (Admin ve todos, RN-24).</summary>
    public async Task<List<SedeDto>> SedesVisiblesAsync(CancellationToken ct)
    {
        var esAdmin = usuario.Rol == "ADMIN";
        var pisos = await db.Pisos
            .Where(p => p.Publicado && (esAdmin || usuario.Pisos.Contains(p.Id)))
            .OrderBy(p => p.Nivel).ToListAsync(ct);
        var sedeIds = pisos.Select(p => p.SedeId).Distinct().ToList();
        var sedes = await db.Sedes.Where(s => sedeIds.Contains(s.Id)).OrderBy(s => s.Nombre).ToListAsync(ct);
        return [.. sedes.Select(s => new SedeDto(s.Id, s.Nombre, s.Direccion,
            [.. pisos.Where(p => p.SedeId == s.Id).Select(p => new PisoDto(p.Id, p.Nombre, p.Nivel, p.AnchoM, p.FondoM, p.Publicado))]))];
    }

    /// <summary>
    /// GET /pisos/{id}/geometria (F-11): solo pisos publicados y solo sus zonas publicadas.
    /// Admin ve todos; los demás, solo los pisos que tienen asignados (RN-24).
    /// </summary>
    public async Task<GeometriaPisoDto> GeometriaAsync(int pisoId, CancellationToken ct)
    {
        var piso = await db.Pisos.SingleOrDefaultAsync(p => p.Id == pisoId && p.Publicado, ct)
            ?? throw new NoEncontradoException($"No existe el piso {pisoId} o aún no está publicado.");
        if (usuario.Rol != "ADMIN" && !usuario.Pisos.Contains(pisoId))
            throw new SinPermisoException("No tienes asignado este piso.");

        var zonas = await db.Zonas
            .Where(z => z.PisoId == pisoId && !z.EnBorrador)
            .OrderBy(z => z.Id)
            .ToListAsync(ct);
        return new GeometriaPisoDto(
            piso.Id, piso.Nombre, piso.Nivel, piso.AnchoM, piso.FondoM, piso.AltoMuroM,
            piso.PlanoRuta is null ? null : "/" + piso.PlanoRuta,
            [.. zonas.Select(z => new ZonaGeometriaDto(
                z.Id, z.Nombre, z.TipoZonaId.Codigo(), z.X, z.Y, z.AnchoM, z.FondoM,
                z.Aforo, z.UmbralMediaPct, z.UmbralAltaPct))]);
    }

    public async Task<IReadOnlyList<int>> SedesDePisosAsync(IEnumerable<int> pisoIds, CancellationToken ct = default)
    {
        var ids = pisoIds.ToList();
        return await db.Pisos.Where(p => ids.Contains(p.Id)).Select(p => p.SedeId).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>?> SedesDelUsuarioAsync(CancellationToken ct = default) =>
        usuario.Rol == "ADMIN" ? null : await SedesDePisosAsync(usuario.Pisos, ct);

    public async Task<char> PrefijoAtencionAsync(int sedeId, CancellationToken ct = default) =>
        await db.Sedes.Where(s => s.Id == sedeId).Select(s => s.PrefijoAtencion).SingleAsync(ct);
}
