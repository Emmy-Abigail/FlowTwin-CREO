using System.Security.Cryptography;
using System.Text;
using FlowTwin.SharedKernel;

namespace FlowTwin.Auditoria.Domain;

/// <summary>
/// aud.Sello: el sello de integridad de una fila de auditoría. Es una tabla aparte y solo de inserción, así la
/// bitácora sigue siendo «solo se inserta» (RN-27): sellar no edita nada.
/// </summary>
public class RegistroSello : EntidadEvento
{
    public long AuditoriaId { get; set; }
    public byte[] Hash { get; set; } = [];
}

/// <summary>Lo que se sella de una fila: todo menos el sello mismo.</summary>
public sealed record DatosSello(
    long Id, DateTime FechaRegistro, int UsuarioRegistroId, int Accion, string Entidad, string? EntidadId,
    string? ValorAntes, string? ValorDespues, string? Ip, string? TraceId);

/// <summary>
/// Cadena de sellos (OWASP: validar la integridad de cada entrada con una función hash criptográfica). Cada sello
/// es un HMAC-SHA256 del contenido de la fila MÁS el sello anterior, con una clave que NO está en la base de datos
/// (user-secrets o variable de entorno). Así, quien edite filas directamente en SQL Server no puede fabricar sellos
/// válidos, y borrar o reordenar filas rompe la cadena. Es pura: se prueba sin base de datos.
/// </summary>
public static class SelladoAuditoria
{
    public const int Tamano = 32;
    public const int ClaveMinimaBytes = 32;

    /// <summary>El «sello anterior» de la primera fila de la cadena.</summary>
    public static readonly byte[] Origen = new byte[Tamano];

    public static byte[] Calcular(byte[] clave, byte[] selloAnterior, DatosSello r)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(selloAnterior);
            w.Write(r.Id);
            w.Write(r.FechaRegistro.Ticks);
            w.Write(r.UsuarioRegistroId);
            w.Write(r.Accion);
            foreach (var texto in new[] { r.Entidad, r.EntidadId, r.ValorAntes, r.ValorDespues, r.Ip, r.TraceId })
                Texto(w, texto);
        }
        return HMACSHA256.HashData(clave, ms.ToArray());
    }

    /// <summary>Con la longitud por delante, para que «ab»+«c» y «a»+«bc» no den lo mismo; nulo y vacío también se distinguen.</summary>
    private static void Texto(BinaryWriter w, string? texto)
    {
        if (texto is null) { w.Write(-1); return; }
        var bytes = Encoding.UTF8.GetBytes(texto);
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    public static bool Coincide(byte[] esperado, byte[] guardado) => CryptographicOperations.FixedTimeEquals(esperado, guardado);

    /// <summary>La clave se guarda en base64 y debe tener al menos 32 bytes; null si no sirve.</summary>
    public static byte[]? LeerClave(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64.Trim());
            return bytes.Length >= ClaveMinimaBytes ? bytes : null;
        }
        catch (FormatException) { return null; }
    }
}
