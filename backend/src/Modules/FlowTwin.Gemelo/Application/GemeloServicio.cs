using FlowTwin.Gemelo.Contracts;
using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Gemelo.Application;

public interface IGemeloDb : IUnidadDeTrabajo
{
    DbSet<Sede> Sedes { get; }
    DbSet<Piso> Pisos { get; }
}

public sealed record PisoDto(int Id, string Nombre, short Nivel, decimal? AnchoM, decimal? FondoM, bool Publicado);
public sealed record SedeDto(int Id, string Nombre, string? Direccion, IReadOnlyList<PisoDto> Pisos);

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
