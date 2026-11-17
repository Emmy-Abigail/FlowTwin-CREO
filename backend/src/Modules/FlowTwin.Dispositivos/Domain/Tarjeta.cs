using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.Dispositivos.Domain;

/// <summary>Catálogo cat.EstadoNodo: el ciclo de vida de un nodo (F-15, RN-20).</summary>
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
    /// <summary>
    /// La última vez que un nodo la detectó y cuál (P9). Se actualiza cada minuto con las lecturas nuevas,
    /// así la pantalla no tiene que recorrer millones de lecturas. Nula: nunca se detectó.
    /// </summary>
    public DateTime? UltimaDeteccionEn { get; set; }
    public int? UltimoNodoId { get; set; }

    /// <summary>Números de tarjeta posibles: el «minor» del emisor BLE va de 1 a 65535 (contrato de integración).</summary>
    public const int NumeroMaximo = 65535;

    /// <summary>
    /// Una tarjeta nueva de la sede: código T-07, identificador BLE {uuid}-{sede}-{número} (major = sede,
    /// minor = número) y texto del QR CREO-T07, el que se graba en la caja.
    /// </summary>
    public static Tarjeta Registrar(int numero, int sedeId)
    {
        if (numero is < 1 or > NumeroMaximo)
            throw new ReglaNegocioException("TARJETA_NUMERO_INVALIDO", $"El número de tarjeta va de 1 a {NumeroMaximo}.");
        return new Tarjeta
        {
            Codigo = $"T-{numero:D2}", CodigoQr = $"CREO-T{numero:D2}", SedeId = sedeId,
            UuidBle = EmisorBle.Identificador(major: sedeId, minor: numero),
        };
    }

    /// <summary>Anota una detección; una más vieja que la guardada no la reemplaza (las lecturas pueden llegar tarde).</summary>
    public void RegistrarDeteccion(DateTime en, int nodoId)
    {
        if (UltimaDeteccionEn is { } antes && antes >= en) return;
        (UltimaDeteccionEn, UltimoNodoId) = (en, nodoId);
    }

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

    /// <summary>"T-07", "t7", "T 7" o "7" → "T-07"; "T-1234" → "T-1234". Otros textos (el contenido del QR) se devuelven en mayúsculas.</summary>
    public static string NormalizarCodigo(string texto)
    {
        var limpio = texto.Trim().ToUpperInvariant();
        var soloNumero = limpio.TrimStart('T').TrimStart(' ', '-');
        return int.TryParse(soloNumero, out var n) && n is > 0 and <= NumeroMaximo ? $"T-{n:D2}" : limpio;
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
