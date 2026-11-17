using FlowTwin.SharedKernel;

namespace FlowTwin.Auditoria.Domain;

/// <summary>
/// aud.Auditoria: una acción que cambió datos (RN-27). Solo se inserta; nadie la edita ni la borra.
/// Quién = UsuarioRegistroId, cuándo = FechaRegistro (las llena el interceptor).
/// </summary>
public class RegistroAuditoria : EntidadEvento
{
    public long Id { get; set; }
    public AccionAuditoria AccionAuditoriaId { get; set; }
    public string Entidad { get; set; } = "";
    public string? EntidadId { get; set; }
    /// <summary>JSON con el estado anterior (nunca contraseñas ni claves).</summary>
    public string? ValorAntes { get; set; }
    public string? ValorDespues { get; set; }
    public string? Ip { get; set; }
    public string? TraceId { get; set; }
}
