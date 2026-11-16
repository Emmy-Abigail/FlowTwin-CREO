using FlowTwin.SharedKernel;

namespace FlowTwin.Atenciones.Domain;

/// <summary>
/// ope.PausaZona: un periodo en que una zona estuvo «fuera de servicio» (limpieza, mantenimiento, sin
/// atención…), RF09 según el ADR-25. Una sola abierta por zona (RN-18). No altera las métricas: los
/// pacientes se siguen detectando; el gemelo la muestra y el historial marca esas horas.
/// </summary>
public class PausaZona : EntidadConActualizacion
{
    public const int DetalleMaximo = 200;

    public int Id { get; set; }
    public int ZonaId { get; set; }
    public MotivoPausa MotivoPausaId { get; set; }
    /// <summary>Obligatorio si el motivo es OTRO.</summary>
    public string? Detalle { get; set; }
    public DateTime InicioEn { get; set; }
    public DateTime? FinEn { get; set; }
    /// <summary>Quién la puso fuera de servicio.</summary>
    public int UsuarioId { get; set; }

    public bool EstaAbierta => FinEn is null;

    public static PausaZona Iniciar(int zonaId, MotivoPausa motivo, string? detalle, int usuarioId, DateTime ahora)
    {
        var texto = string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim();
        if (motivo == MotivoPausa.Otro && texto is null)
            throw new ReglaNegocioException("PAUSA_SIN_DETALLE", "Con el motivo «Otro», escribe cuál es.");
        if (texto?.Length > DetalleMaximo)
            throw new ReglaNegocioException("PAUSA_DETALLE_LARGO", $"El detalle admite hasta {DetalleMaximo} caracteres.");
        return new PausaZona { ZonaId = zonaId, MotivoPausaId = motivo, Detalle = texto, UsuarioId = usuarioId, InicioEn = ahora };
    }

    public void Finalizar(DateTime ahora)
    {
        if (!EstaAbierta) throw new ConflictoException("La zona ya estaba en servicio.");
        FinEn = ahora < InicioEn ? InicioEn : ahora;
    }
}
