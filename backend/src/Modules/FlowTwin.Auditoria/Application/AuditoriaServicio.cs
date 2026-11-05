using System.Text.Json;
using FlowTwin.Auditoria.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Auditoria.Application;

public interface IAuditoriaDb : IUnidadDeTrabajo
{
    DbSet<RegistroAuditoria> Auditoria { get; }
}

/// <summary>
/// Implementa IAuditoria: agrega la fila al mismo contexto, así se guarda en la misma transacción
/// que el cambio que la originó. Si el cambio falla, tampoco queda la fila de auditoría.
/// </summary>
public sealed class AuditoriaServicio(IAuditoriaDb db, IUsuarioActual usuario) : IAuditoria
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Registrar(AccionAuditoria accion, string entidad, string? entidadId, object? antes = null, object? despues = null)
    {
        db.Auditoria.Add(new RegistroAuditoria
        {
            AccionAuditoriaId = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            ValorAntes = antes is null ? null : JsonSerializer.Serialize(antes, Json),
            ValorDespues = despues is null ? null : JsonSerializer.Serialize(despues, Json),
            Ip = usuario.Ip,
            TraceId = usuario.TraceId,
        });
    }
}
