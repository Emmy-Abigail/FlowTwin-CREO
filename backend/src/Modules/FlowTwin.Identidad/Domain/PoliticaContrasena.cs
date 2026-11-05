using System.Security.Cryptography;

namespace FlowTwin.Identidad.Domain;

/// <summary>
/// Política de contraseñas (sección Seguridad → Contraseñas): mínimo 8 caracteres con mayúscula,
/// minúscula, número y símbolo; no puede contener el correo ni el nombre.
/// La regla "no repite las 3 últimas" se comprueba en el caso de uso, porque necesita los hashes.
/// </summary>
public static class PoliticaContrasena
{
    public const int LongitudMinima = 8;

    /// <summary>Devuelve los requisitos que NO se cumplen (vacío = válida).</summary>
    public static IReadOnlyList<string> Incumplidos(string clave, string correo, string nombres, string apellidos)
    {
        var faltan = new List<string>();
        if (clave.Length < LongitudMinima) faltan.Add($"Debe tener al menos {LongitudMinima} caracteres.");
        if (!clave.Any(char.IsUpper)) faltan.Add("Debe tener al menos una mayúscula.");
        if (!clave.Any(char.IsLower)) faltan.Add("Debe tener al menos una minúscula.");
        if (!clave.Any(char.IsDigit)) faltan.Add("Debe tener al menos un número.");
        if (clave.All(char.IsLetterOrDigit)) faltan.Add("Debe tener al menos un símbolo.");

        var usuarioCorreo = correo.Split('@')[0];
        var palabras = new[] { usuarioCorreo }
            .Concat(nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Concat(apellidos.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(p => p.Length >= 3);
        if (palabras.Any(p => clave.Contains(p, StringComparison.OrdinalIgnoreCase)))
            faltan.Add("No puede contener tu correo ni tu nombre.");
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
