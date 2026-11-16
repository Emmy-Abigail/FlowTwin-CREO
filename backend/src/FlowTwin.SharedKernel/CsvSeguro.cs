namespace FlowTwin.SharedKernel;

/// <summary>Escritura segura de un campo CSV, común a todos los reportes.</summary>
public static class CsvSeguro
{
    /// <summary>
    /// Comillas si hace falta (RFC 4180) y un apóstrofo delante de lo que Excel tomaría como fórmula
    /// (=, +, -, @): la protección no depende de recordar que un campo viene de una persona.
    /// </summary>
    public static string Campo(string valor)
    {
        if (valor.Length > 0 && "=+-@\t\r".Contains(valor[0])) valor = "'" + valor;
        return valor.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
    }
}
