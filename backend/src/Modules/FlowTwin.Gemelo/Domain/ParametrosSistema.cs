using FlowTwin.SharedKernel;

namespace FlowTwin.Gemelo.Domain;

/// <summary>
/// gem.ParametrosSistema: los valores que el Administrador ajusta en Configuración › Parámetros (ADR-27).
/// Una sola fila (Id = 1). Los valores iniciales son los por defecto que ya usaba el sistema.
/// </summary>
public class ParametrosSistema : EntidadConActualizacion
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;
    /// <summary>Espera desde la que hay alerta (ámbar).</summary>
    public short EsperaAltaMin { get; set; } = 40;
    /// <summary>Espera desde la que la alerta es crítica (roja, suena).</summary>
    public short EsperaCriticaMin { get; set; } = 60;
    /// <summary>Minutos sin detectar a un paciente para avisar «sin lecturas».</summary>
    public short SinLecturasMin { get; set; } = 30;
    /// <summary>Hora local (0–23) del cierre de jornada (RN-08).</summary>
    public byte HoraCierre { get; set; } = 22;
    /// <summary>% del aforo desde el que una sala de espera se ve ámbar («se está llenando»). El rojo es al superar el aforo.</summary>
    public byte SemaforoAmbarPct { get; set; } = 80;

    public IReadOnlyList<string> Problemas()
    {
        var p = new List<string>();
        if (EsperaAltaMin is < 5 or > 600) p.Add("La alerta de espera va de 5 a 600 minutos.");
        if (EsperaCriticaMin <= EsperaAltaMin || EsperaCriticaMin > 600) p.Add("La espera crítica debe ser mayor que la de alerta (hasta 600).");
        if (SinLecturasMin is < 5 or > 600) p.Add("«Sin lecturas» va de 5 a 600 minutos.");
        if (HoraCierre > 23) p.Add("La hora de cierre va de 0 a 23.");
        if (SemaforoAmbarPct is < 10 or > 100) p.Add("El ámbar del semáforo va de 10 % a 100 % del aforo.");
        return p;
    }

    public void Validar()
    {
        var p = Problemas();
        if (p.Count > 0) throw new ReglaNegocioException("PARAMETROS_INVALIDOS", "Algún parámetro no es válido.", new { problemas = p });
    }
}
