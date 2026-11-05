using FlowTwin.SharedKernel;

namespace FlowTwin.Atenciones.Domain;

/// <summary>Catálogo cat.MotivoCierreAtencion. ANULADA no cuenta en las métricas (RN-04).</summary>
public enum MotivoCierreAtencion
{
    [Catalogo("DEVUELTA", "Devuelta")] Devuelta = 1,
    [Catalogo("SIN_LECTURAS", "Sin lecturas")] SinLecturas = 2,
    [Catalogo("CIERRE_JORNADA", "Cierre de jornada")] CierreJornada = 3,
    [Catalogo("ANULADA", "Anulada")] Anulada = 4,
}

/// <summary>Catálogo cat.MotivoPausa (las pausas de zona llegan en el Avance 3).</summary>
public enum MotivoPausa
{
    [Catalogo("LIMPIEZA", "Limpieza")] Limpieza = 1,
    [Catalogo("MANTENIMIENTO", "Mantenimiento")] Mantenimiento = 2,
    [Catalogo("SIN_ATENCION", "Sin atención")] SinAtencion = 3,
    [Catalogo("OTRO", "Otro")] Otro = 4,
}

/// <summary>
/// ope.Atencion: el recorrido de un paciente, de la entrega a la devolución de la tarjeta.
/// Solo tiene un código anónimo (A-0143): nada la vincula con una persona (Ley 29733).
/// No se da de baja: se cierra con un motivo, y ANULADA es su forma de "deshacer".
/// </summary>
public class Atencion : EntidadConActualizacion
{
    /// <summary>RN-04: una entrega se puede deshacer solo en los 30 segundos siguientes.</summary>
    public static readonly TimeSpan VentanaDeshacer = TimeSpan.FromSeconds(30);

    public long Id { get; set; }
    public string Codigo { get; set; } = "";
    /// <summary>Día de la atención en hora de Lima: el código es correlativo por sede y día (RN-02).</summary>
    public DateOnly FechaAtencion { get; set; }
    public int SedeId { get; set; }
    public int TarjetaId { get; set; }
    public DateTime InicioEn { get; set; }
    public DateTime? FinEn { get; set; }
    public MotivoCierreAtencion? MotivoCierreId { get; set; }
    /// <summary>Quién entregó la tarjeta es UsuarioRegistroId; quién la recibió, este campo.</summary>
    public int? RecibidaPor { get; set; }
    public DateTime? UltimaLecturaEn { get; set; }

    public bool EstaAbierta => FinEn is null;

    public static Atencion Abrir(string codigo, DateOnly fecha, int sedeId, int tarjetaId, DateTime ahora) => new()
    {
        Codigo = codigo, FechaAtencion = fecha, SedeId = sedeId, TarjetaId = tarjetaId, InicioEn = ahora,
    };

    /// <summary>RN-05: una atención cerrada no se reabre.</summary>
    public void Cerrar(MotivoCierreAtencion motivo, DateTime ahora, int? recibidaPor = null)
    {
        if (!EstaAbierta)
            throw new ReglaNegocioException("RN05_ATENCION_CERRADA", $"La atención {Codigo} ya está cerrada.");
        FinEn = ahora;
        MotivoCierreId = motivo;
        RecibidaPor = recibidaPor;
    }

    /// <summary>RN-04: deshacer la entrega dentro de los 30 s; la atención queda ANULADA.</summary>
    public void Anular(DateTime ahora)
    {
        if (ahora - InicioEn > VentanaDeshacer)
            throw new ReglaNegocioException("RN04_DESHACER_VENCIDO",
                "Ya pasaron los 30 segundos para deshacer esta entrega.");
        Cerrar(MotivoCierreAtencion.Anulada, ahora);
    }

    public int TotalMinutos(DateTime ahora) => (int)Math.Round(((FinEn ?? ahora) - InicioEn).TotalMinutes);

    /// <summary>A-0001, A-0002… (RN-02).</summary>
    public static string FormatearCodigo(char prefijo, int correlativo) => $"{prefijo}-{correlativo:D4}";
}
