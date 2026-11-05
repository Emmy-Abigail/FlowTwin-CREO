using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlowTwin.SharedKernel;

namespace FlowTwin.Dispositivos.Domain;

/// <summary>
/// dis.Nodo: un ESP32 receptor fijo en una zona. Escucha a los emisores BLE cercanos y envía al
/// servidor qué identificador vio y con qué señal. Cubre una sola zona; una zona puede tener varios (RN-19).
/// BAJA es un estado de su ciclo de vida (retirado o dañado): su clave deja de servir.
/// </summary>
public class Nodo : EntidadEditable
{
    public int Id { get; set; }
    /// <summary>«N-01».</summary>
    public string Codigo { get; set; } = "";
    /// <summary>MAC del ESP32 en formato XX:XX:XX:XX:XX:XX.</summary>
    public string Mac { get; set; } = "";
    public int ZonaId { get; set; }
    /// <summary>Ubicación en el plano, en metros. Nula mientras no se sepa dónde se instaló.</summary>
    public decimal? PosX { get; set; }
    public decimal? PosY { get; set; }
    /// <summary>SHA-256 de la clave del nodo. La clave misma nunca se guarda.</summary>
    public byte[] ClaveApiHash { get; set; } = [];
    /// <summary>Recién registrado está Sin señal: pasa a En línea con su primer latido (F-15).</summary>
    public EstadoNodo EstadoNodoId { get; set; } = EstadoNodo.SinSenal;
    public DateTime? UltimoLatidoEn { get; set; }
    public string? VersionFirmware { get; set; }
    /// <summary>Calibración en sitio (ADR-20): dBm que se suman a la señal de este nodo, de -20 a 20.</summary>
    public short AjusteRssiDb { get; set; }

    public const int AjusteMaximoDb = 20;

    /// <summary>Genera una clave nueva, guarda solo su hash y devuelve la clave para mostrarla una vez.</summary>
    public string AsignarClaveNueva()
    {
        var clave = ClaveNodo.Generar();
        ClaveApiHash = ClaveNodo.Hash(clave);
        return clave;
    }

    /// <summary>Compara en tiempo constante, para no revelar con la demora cuántos bytes acertó un atacante.</summary>
    public bool ClaveCoincide(string clave) =>
        EstadoNodoId != EstadoNodo.Baja && CryptographicOperations.FixedTimeEquals(ClaveNodo.Hash(clave), ClaveApiHash);

    /// <summary>Cambia de estado y devuelve el evento para el historial (null si ya estaba en ese estado).</summary>
    public EventoNodo? CambiarA(EstadoNodo nuevo, string? detalle = null)
    {
        if (EstadoNodoId == nuevo) return null;
        if (EstadoNodoId == EstadoNodo.Baja)
            throw new ReglaNegocioException("RN_NODO_DE_BAJA", $"El nodo {Codigo} está de baja y ya no cambia de estado.");
        EstadoNodoId = nuevo;
        return new EventoNodo { NodoId = Id, EstadoNodoId = nuevo, Detalle = detalle };
    }

    /// <summary>Retira el nodo: su clave queda reemplazada por otra que nadie conoce.</summary>
    public EventoNodo DarDeBaja(string? detalle = null)
    {
        var evento = CambiarA(EstadoNodo.Baja, detalle)
            ?? throw new ConflictoException($"El nodo {Codigo} ya está de baja.");
        AsignarClaveNueva();
        return evento;
    }

    /// <summary>Lo que no cumple el nodo, para el formulario. Vacía = válido.</summary>
    public IReadOnlyList<string> Problemas()
    {
        var problemas = new List<string>();
        if (!Regex.IsMatch(Codigo, @"^N-\d{2,3}$")) problemas.Add("El código del nodo debe tener la forma N-01.");
        if (NormalizarMac(Mac) != Mac) problemas.Add("La MAC debe tener la forma A4:CF:12:9B:3E:03.");
        if (ZonaId <= 0) problemas.Add("El nodo necesita una zona.");
        if (PosX is < 0 || PosY is < 0) problemas.Add("La posición del nodo no puede ser negativa.");
        if (Math.Abs((int)AjusteRssiDb) > AjusteMaximoDb) problemas.Add($"El ajuste de señal va de -{AjusteMaximoDb} a {AjusteMaximoDb} dBm.");
        return problemas;
    }

    public void Validar()
    {
        var problemas = Problemas();
        if (problemas.Count > 0)
            throw new ReglaNegocioException("NODO_INVALIDO", $"El nodo {Codigo} no es válido.", new { problemas });
    }

    /// <summary>"n3", "N 3" o "3" → "N-03". Otro texto se devuelve en mayúsculas para que la validación lo rechace.</summary>
    public static string NormalizarCodigo(string texto)
    {
        var limpio = texto.Trim().ToUpperInvariant();
        var soloNumero = limpio.TrimStart('N').TrimStart(' ', '-');
        return int.TryParse(soloNumero, out var n) && n is > 0 and < 1000 ? $"N-{n:D2}" : limpio;
    }

    /// <summary>"a4cf129b3e03", "A4-CF-12-9B-3E-03"… → "A4:CF:12:9B:3E:03". null si no son 12 dígitos hexadecimales.</summary>
    public static string? NormalizarMac(string texto)
    {
        var hex = Regex.Replace(texto.Trim().ToUpperInvariant(), "[:\\-. ]", "");
        if (!Regex.IsMatch(hex, "^[0-9A-F]{12}$")) return null;
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
    }
}

/// <summary>
/// Claves de los nodos: 32 bytes aleatorios con el prefijo «nk_» (node key), para reconocerlas
/// si aparecen en un log o en un archivo. Se guarda su SHA-256: a diferencia de una contraseña
/// humana, una clave aleatoria de 32 bytes no se puede adivinar, así que no hace falta un hash
/// lento como PBKDF2, y la ingesta necesita verificar rápido en cada petición.
/// </summary>
public static class ClaveNodo
{
    public const string Prefijo = "nk_";

    public static string Generar() =>
        Prefijo + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string clave) => SHA256.HashData(Encoding.UTF8.GetBytes(clave));
}

/// <summary>dis.EventoNodo: historial de estados del nodo. Inmutable; FechaRegistro dice cuándo.</summary>
public class EventoNodo : EntidadEvento
{
    public long Id { get; set; }
    public int NodoId { get; set; }
    public EstadoNodo EstadoNodoId { get; set; }
    /// <summary>«Sin latido desde 15:10».</summary>
    public string? Detalle { get; set; }
}
