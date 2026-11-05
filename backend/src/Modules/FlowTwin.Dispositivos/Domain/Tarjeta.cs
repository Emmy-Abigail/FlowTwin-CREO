using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.Dispositivos.Domain;

/// <summary>Catálogo cat.EstadoNodo (los nodos llegan en el Avance 2).</summary>
public enum EstadoNodo
{
    [Catalogo("EN_LINEA", "En línea")] EnLinea = 1,
    [Catalogo("SIN_SENAL", "Sin señal")] SinSenal = 2,
    [Catalogo("BAJA", "De baja")] Baja = 3,
}

/// <summary>
/// dis.Tarjeta. Su ciclo de vida es el corazón del sistema: cada atención nace y termina con un
/// cambio de estado de la tarjeta. BAJA es un estado del ciclo de vida (tarjeta dañada o perdida);
/// EstadoRegistro = 0 en cambio significa que el registro mismo se anuló (RN-28).
/// </summary>
public class Tarjeta : EntidadEditable
{
    /// <summary>
    /// Transiciones permitidas (diagrama de estados del documento). Cualquier otra se rechaza
    /// con RN_TRANSICION_INVALIDA.
    /// </summary>
    private static readonly Dictionary<EstadoTarjeta, EstadoTarjeta[]> Permitidas = new()
    {
        [EstadoTarjeta.Disponible] = [EstadoTarjeta.EnUso, EstadoTarjeta.Baja],
        [EstadoTarjeta.EnUso] = [EstadoTarjeta.PorDesinfectar, EstadoTarjeta.Disponible, EstadoTarjeta.SinDevolver],
        [EstadoTarjeta.PorDesinfectar] = [EstadoTarjeta.Disponible, EstadoTarjeta.Baja],
        [EstadoTarjeta.SinDevolver] = [EstadoTarjeta.PorDesinfectar, EstadoTarjeta.Extraviada],
        [EstadoTarjeta.Extraviada] = [EstadoTarjeta.PorDesinfectar, EstadoTarjeta.Baja],
        [EstadoTarjeta.Baja] = [],
    };

    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string UuidBle { get; set; } = "";
    public string CodigoQr { get; set; } = "";
    public int SedeId { get; set; }
    public EstadoTarjeta EstadoTarjetaId { get; set; } = EstadoTarjeta.Disponible;

    public static bool PuedePasar(EstadoTarjeta desde, EstadoTarjeta hacia) => Permitidas[desde].Contains(hacia);

    /// <summary>Cambia de estado y devuelve el movimiento que lo deja registrado.</summary>
    public MovimientoTarjeta CambiarA(EstadoTarjeta nuevo, long? atencionId = null, string? observacion = null)
    {
        if (!PuedePasar(EstadoTarjetaId, nuevo))
            throw new ReglaNegocioException("RN_TRANSICION_INVALIDA",
                $"La tarjeta {Codigo} no puede pasar de «{EstadoTarjetaId.Nombre()}» a «{nuevo.Nombre()}».",
                new { tarjeta = Codigo, estado = EstadoTarjetaId.Codigo() });
        var movimiento = new MovimientoTarjeta
        {
            TarjetaId = Id, EstadoAnteriorId = EstadoTarjetaId, EstadoNuevoId = nuevo,
            AtencionId = atencionId, Observacion = observacion,
        };
        EstadoTarjetaId = nuevo;
        return movimiento;
    }

    /// <summary>"T-07", "t7", "T 7" o "7" → "T-07". Otros textos (el contenido del QR) se devuelven en mayúsculas.</summary>
    public static string NormalizarCodigo(string texto)
    {
        var limpio = texto.Trim().ToUpperInvariant();
        var soloNumero = limpio.TrimStart('T').TrimStart(' ', '-');
        return int.TryParse(soloNumero, out var n) && n is > 0 and < 1000 ? $"T-{n:D2}" : limpio;
    }
}

/// <summary>dis.MovimientoTarjeta: cada cambio de estado. Inmutable; FechaRegistro y UsuarioRegistroId dicen cuándo y quién.</summary>
public class MovimientoTarjeta : EntidadEvento
{
    public long Id { get; set; }
    public int TarjetaId { get; set; }
    public EstadoTarjeta? EstadoAnteriorId { get; set; }
    public EstadoTarjeta EstadoNuevoId { get; set; }
    public long? AtencionId { get; set; }
    public string? Observacion { get; set; }
}
