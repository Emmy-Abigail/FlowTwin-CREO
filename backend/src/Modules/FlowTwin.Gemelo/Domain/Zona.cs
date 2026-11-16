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
    /// <summary>
    /// Capacidad de la zona (RN-13, ADR-27): en Ingreso y Espera, las personas que caben (el aforo oficial, ITSE);
    /// en Triaje y Consulta, los puestos de atención (1 médico, 4 camillas): es la capacidad del servicio y lo que
    /// deja ver el cuello de botella. Tránsito y Salida no tienen. Nulo = «aforo por definir». El semáforo usa un
    /// umbral general (Parámetros), no uno por zona.
    /// </summary>
    public short? Aforo { get; set; }
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

        // RN-13 (ADR-27): el aforo existe donde el paciente permanece; puede quedar por definir (nulo)
        if (TipoZonaId.TieneAforo())
        {
            if (Aforo is < 1) problemas.Add("El aforo debe ser de 1 o más personas.");
        }
        else if (Aforo is not null)
        {
            problemas.Add($"Una zona de tipo {TipoZonaId.Nombre()} no tiene aforo: es de paso.");
        }
        return problemas;
    }

    /// <summary>
    /// Una zona nueva desde Configuración. El rectángulo (X, Y, Ancho, Fondo) es del plano 2D anterior y el
    /// gemelo 3D no lo usa: se guarda uno mínimo hasta quitar esas columnas (limpieza de muros y planoUrl).
    /// </summary>
    public static Zona Nueva(int pisoId, string nombre, TipoZona tipo, short? aforo) => new()
    {
        PisoId = pisoId, Nombre = nombre.Trim(), TipoZonaId = tipo, Aforo = tipo.TieneAforo() ? aforo : null,
        X = 0, Y = 0, AnchoM = 1, FondoM = 1, EnBorrador = false,
    };

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
    /// <summary>
    /// RN-13 (ADR-27): tienen aforo y semáforo las zonas donde el paciente permanece. En Triaje y Consulta el aforo
    /// es el número de puestos: 0/1 verde (libre), 1/1 ámbar (sin espacio). Tránsito y Salida, nunca.
    /// </summary>
    public static bool TieneAforo(this TipoZona tipo) => tipo is not (TipoZona.Transito or TipoZona.Salida);
}
