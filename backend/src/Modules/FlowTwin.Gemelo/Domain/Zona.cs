using FlowTwin.SharedKernel;

namespace FlowTwin.Gemelo.Domain;

/// <summary>
/// gem.Zona: un área monitoreada del piso. En el MVP es un rectángulo en metros (X, Y es la esquina
/// superior izquierda); Geometria queda para los polígonos libres de la fase 2.
/// </summary>
public class Zona : EntidadEditable
{
    public int Id { get; set; }
    public int PisoId { get; set; }
    public TipoZona TipoZonaId { get; set; }
    public string Nombre { get; set; } = "";
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal AnchoM { get; set; }
    public decimal FondoM { get; set; }
    /// <summary>Polígono libre en JSON (fase 2); si existe, manda sobre el rectángulo.</summary>
    public string? Geometria { get; set; }
    /// <summary>Personas que caben. Nulo en Tránsito y Salida (RN-13).</summary>
    public short? Aforo { get; set; }
    /// <summary>% del aforo desde el que la zona pasa de concentración baja a media.</summary>
    public byte? UmbralMediaPct { get; set; }
    /// <summary>% del aforo desde el que la zona pasa de concentración media a alta.</summary>
    public byte? UmbralAltaPct { get; set; }
    /// <summary>
    /// true = aún no publicada: el gemelo en vivo no la muestra. Sin default en la base a propósito:
    /// con un bool, EF no envía el valor false y la base pondría el default (la misma trampa que
    /// DebeCambiarContrasena en seg.Usuario).
    /// </summary>
    public bool EnBorrador { get; set; } = true;

    /// <summary>
    /// Lo que no cumple la zona, en palabras para el Administrador. Vacía = válida.
    /// Se devuelve la lista completa (no solo el primer error) porque al publicar un piso
    /// el editor muestra todos los problemas juntos (F-14).
    /// </summary>
    public IReadOnlyList<string> Problemas()
    {
        var problemas = new List<string>();
        if (string.IsNullOrWhiteSpace(Nombre)) problemas.Add("La zona necesita un nombre.");
        else if (Nombre.Length > 80) problemas.Add("El nombre de la zona admite hasta 80 caracteres.");
        if (X < 0 || Y < 0) problemas.Add("La posición de la zona no puede ser negativa.");
        if (AnchoM <= 0 || FondoM <= 0) problemas.Add("El ancho y el fondo de la zona deben ser mayores que 0.");

        // RN-13: solo las zonas donde la gente permanece tienen aforo y semáforo
        if (TipoZonaId.TieneAforo())
        {
            if (Aforo is null or < 1) problemas.Add($"Una zona de tipo {TipoZonaId.Nombre()} necesita un aforo de 1 o más (RN-13).");
            if (UmbralMediaPct is null || UmbralAltaPct is null)
                problemas.Add("La zona necesita los umbrales de concentración media y alta.");
            else if (!(UmbralMediaPct > 0 && UmbralMediaPct <= UmbralAltaPct && UmbralAltaPct <= 100))
                problemas.Add("Los umbrales deben cumplir 0 < media ≤ alta ≤ 100.");
        }
        else if (Aforo is not null || UmbralMediaPct is not null || UmbralAltaPct is not null)
        {
            problemas.Add($"Una zona de tipo {TipoZonaId.Nombre()} no tiene aforo ni umbrales (RN-13).");
        }
        return problemas;
    }

    /// <summary>Lanza RN13_ZONA_INVALIDA con todos los problemas si la zona no es válida.</summary>
    public void Validar()
    {
        var problemas = Problemas();
        if (problemas.Count > 0)
            throw new ReglaNegocioException("RN13_ZONA_INVALIDA", $"La zona «{Nombre}» no es válida.", new { problemas });
    }
}

public static class TipoZonaExtensiones
{
    /// <summary>RN-13: Ingreso, Espera y Servicio tienen aforo y color; Tránsito y Salida no.</summary>
    public static bool TieneAforo(this TipoZona tipo) =>
        tipo is TipoZona.Ingreso or TipoZona.Espera or TipoZona.Servicio;
}
