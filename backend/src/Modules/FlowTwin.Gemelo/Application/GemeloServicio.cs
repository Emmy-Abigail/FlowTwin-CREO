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
    DbSet<ParametrosSistema> ParametrosSistema { get; }
}

public sealed record PisoDto(int Id, string Nombre, short Nivel, decimal? AnchoM, decimal? FondoM, bool Publicado);
public sealed record SedeDto(int Id, string Nombre, string? Direccion, IReadOnlyList<PisoDto> Pisos);

/// <summary>Una zona tal como la dibujan la vista 2D y la 3D (sección "La geometría que recibe el navegador").</summary>
public sealed record ZonaGeometriaDto(
    int Id, string Nombre, string Tipo, decimal X, decimal Y, decimal Ancho, decimal Fondo, short? Aforo);

/// <summary>GET /pisos/{id}/geometria: medidas del piso, sus zonas y sus nodos. Lo que se dibuja viene de la maqueta 3D.</summary>
public sealed record GeometriaPisoDto(
    int PisoId, string Nombre, short Nivel, decimal? AnchoM, decimal? FondoM,
    IReadOnlyList<ZonaGeometriaDto> Zonas, IReadOnlyList<NodoEnPlano> Nodos);

public sealed class GemeloServicio(IGemeloDb db, IUsuarioActual usuario, IUbicacionNodos nodos) : ISedesConsultas
{
    /// <summary>GET /sedes: sedes y pisos publicados que el usuario puede ver (Admin y Gerencia ven todos, RN-24).</summary>
    public async Task<List<SedeDto>> SedesVisiblesAsync(CancellationToken ct)
    {
        var veTodos = usuario.VeTodosLosPisos();
        var pisos = await db.Pisos
            .Where(p => p.Publicado && (veTodos || usuario.Pisos.Contains(p.Id)))
            .OrderBy(p => p.Nivel).ToListAsync(ct);
        var sedeIds = pisos.Select(p => p.SedeId).Distinct().ToList();
        var sedes = await db.Sedes.Where(s => sedeIds.Contains(s.Id)).OrderBy(s => s.Nombre).ToListAsync(ct);
        return [.. sedes.Select(s => new SedeDto(s.Id, s.Nombre, s.Direccion,
            [.. pisos.Where(p => p.SedeId == s.Id).Select(p => new PisoDto(p.Id, p.Nombre, p.Nivel, p.AnchoM, p.FondoM, p.Publicado))]))];
    }

    /// <summary>
    /// GET /pisos/{id}/geometria (F-11): solo pisos publicados y solo sus zonas publicadas.
    /// Admin y Gerencia ven todos; el Operador, solo los pisos que tiene asignados (RN-24).
    /// </summary>
    public async Task<GeometriaPisoDto> GeometriaAsync(int pisoId, CancellationToken ct)
    {
        var piso = await db.Pisos.SingleOrDefaultAsync(p => p.Id == pisoId && p.Publicado, ct)
            ?? throw new NoEncontradoException($"No existe el piso {pisoId} o aún no está publicado.");
        if (!usuario.PuedeVerPiso(pisoId))
            throw new SinPermisoException("No tienes asignado este piso.");

        var zonas = await db.Zonas
            .Where(z => z.PisoId == pisoId && !z.EnBorrador)
            .OrderBy(z => z.Id)
            .ToListAsync(ct);
        return new GeometriaPisoDto(
            piso.Id, piso.Nombre, piso.Nivel, piso.AnchoM, piso.FondoM,
            [.. zonas.Select(z => new ZonaGeometriaDto(
                z.Id, z.Nombre, z.TipoZonaId.Codigo(), z.X, z.Y, z.AnchoM, z.FondoM,
                z.Aforo))],
            await nodos.DeZonasAsync([.. zonas.Select(z => z.Id)], ct));
    }

    public async Task<IReadOnlyList<int>> SedesDePisosAsync(IEnumerable<int> pisoIds, CancellationToken ct = default)
    {
        var ids = pisoIds.ToList();
        return await db.Pisos.Where(p => ids.Contains(p.Id)).Select(p => p.SedeId).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>?> SedesDelUsuarioAsync(CancellationToken ct = default) =>
        usuario.VeTodosLosPisos() ? null : await SedesDePisosAsync(usuario.Pisos, ct);

    public async Task<string?> NombreSedeAsync(int sedeId, CancellationToken ct = default) =>
        await db.Sedes.IgnoreQueryFilters().Where(s => s.Id == sedeId).Select(s => s.Nombre).SingleOrDefaultAsync(ct);

    public async Task<char> PrefijoAtencionAsync(int sedeId, CancellationToken ct = default) =>
        await db.Sedes.Where(s => s.Id == sedeId).Select(s => s.PrefijoAtencion).SingleAsync(ct);
}
