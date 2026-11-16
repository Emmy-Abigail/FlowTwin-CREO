using FlowTwin.Auditoria.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Identidad.Application;

/// <summary>Implementa el contrato de Auditoría: quién hizo cada acción, también si ya fue dado de baja (RN-29).</summary>
public sealed class NombresDeUsuarios(IIdentidadDb db) : INombresDeUsuarios
{
    public async Task<IReadOnlyDictionary<int, string>> NombresAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var buscados = ids.Where(id => id != UsuarioSistema.Id).Distinct().ToList();
        var usuarios = await db.Usuarios.IgnoreQueryFilters().AsNoTracking().Where(u => buscados.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombres, u.Apellidos }).ToListAsync(ct);
        var nombres = usuarios.ToDictionary(u => u.Id, u => $"{u.Nombres} {u.Apellidos}".Trim());
        nombres[UsuarioSistema.Id] = "Sistema";
        return nombres;
    }
}
