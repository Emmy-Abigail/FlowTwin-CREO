using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowTwin.Identidad.Domain;

/// <summary>
/// Política de contraseñas según OWASP ASVS 4.0.3 (V2.1) y NIST SP 800-63B: lo que protege es la LONGITUD y que no
/// sea una contraseña conocida, no obligar a combinar mayúsculas, números y símbolos (eso produce «Clave2026!»,
/// fácil de adivinar). Se aceptan espacios y tildes: una frase larga es más segura y más fácil de recordar.
/// La regla «no repite las 3 últimas» se comprueba en el caso de uso, porque necesita los hashes.
/// </summary>
public static class PoliticaContrasena
{
    public const int LongitudMinima = 12;
    /// <summary>Tope alto (ASVS pide permitir al menos 64): evita que alguien mande textos enormes para cargar el servidor.</summary>
    public const int LongitudMaxima = 128;

    /// <summary>
    /// Palabras propias del sistema (NIST: «palabras del contexto»). Se rechaza una contraseña que sea SOLO una de
    /// ellas con números o símbolos («flowtwin2026!»); dentro de una frase sí se pueden usar.
    /// </summary>
    private static readonly Regex SoloPalabraDelContexto = new(
        @"^[\W\d_]*(flowtwin|creo|clinica|clínica|cayetano|upch|contrasena|contraseña|password|admin)+[\W\d_]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Contraseñas filtradas en ataques reales (SecLists, licencia MIT), de 12 caracteres o más y en minúsculas. Se
    /// cargan una sola vez desde el propio ensamblado: no se consulta nada en internet.
    /// </summary>
    private static readonly Lazy<HashSet<string>> Filtradas = new(() =>
    {
        using var recurso = typeof(PoliticaContrasena).Assembly.GetManifestResourceStream("FlowTwin.Identidad.contrasenas-filtradas.txt.gz")
            ?? throw new InvalidOperationException("Falta la lista de contraseñas filtradas en el ensamblado.");
        using var lector = new StreamReader(new GZipStream(recurso, CompressionMode.Decompress), Encoding.UTF8);
        var lista = new HashSet<string>(StringComparer.Ordinal);
        while (lector.ReadLine() is { } linea)
            if (linea.Length > 0 && linea[0] != '#') lista.Add(linea);
        return lista;
    });

    /// <summary>Devuelve los requisitos que NO se cumplen (vacío = válida).</summary>
    public static IReadOnlyList<string> Incumplidos(string clave, string correo, string nombres, string apellidos)
    {
        var faltan = new List<string>();
        if (clave.Length < LongitudMinima) faltan.Add($"Debe tener al menos {LongitudMinima} caracteres.");
        if (clave.Length > LongitudMaxima) faltan.Add($"Puede tener como máximo {LongitudMaxima} caracteres.");
        if (clave.Distinct().Count() < 4) faltan.Add("Es demasiado repetitiva: usa una frase o más variedad de caracteres.");

        var usuarioCorreo = correo.Split('@')[0];
        var palabras = new[] { usuarioCorreo }
            .Concat(nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Concat(apellidos.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(p => p.Length >= 3);
        if (palabras.Any(p => clave.Contains(p, StringComparison.OrdinalIgnoreCase)))
            faltan.Add("No puede contener tu correo ni tu nombre.");

        if (clave.Length is >= LongitudMinima and <= LongitudMaxima
            && (SoloPalabraDelContexto.IsMatch(clave) || Filtradas.Value.Contains(clave.ToLowerInvariant())))
            faltan.Add("Es una contraseña conocida o fácil de adivinar: elige otra.");
        return faltan;
    }

    /// <summary>Contraseña temporal de 12 caracteres que siempre cumple la política (F-04).</summary>
    public static string GenerarTemporal()
    {
        const string mayus = "ABCDEFGHJKLMNPQRSTUVWXYZ", minus = "abcdefghijkmnopqrstuvwxyz",
                     numeros = "23456789", simbolos = "#$%&*!?";
        const string todos = mayus + minus + numeros + simbolos;
        var c = new List<char>
        {
            mayus[RandomNumberGenerator.GetInt32(mayus.Length)],
            minus[RandomNumberGenerator.GetInt32(minus.Length)],
            numeros[RandomNumberGenerator.GetInt32(numeros.Length)],
            simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)],
        };
        while (c.Count < 12) c.Add(todos[RandomNumberGenerator.GetInt32(todos.Length)]);
        return new string([.. c.OrderBy(_ => RandomNumberGenerator.GetInt32(1000))]);
    }
}
