using System.Text;
using FlowTwin.Atenciones.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.Reportes.Domain;

/// <summary>
/// El CSV de atenciones del historial: anónimo (solo el código, Ley 29733), con las horas en hora de Lima y
/// con BOM UTF-8 para que Excel muestre bien las tildes. Separador coma (RFC 4180).
/// </summary>
public static class CsvHistorial
{
    private static readonly string[] Encabezado =
        ["Código", "Fecha", "Entrega", "Fin", "Estado", "Cierre", "Espera (min)", "Consulta (min)", "Total (min)"];

    private static readonly Dictionary<string, string> Estados = new()
    {
        [EstadoAtencion.EnEspera] = "En espera",
        [EstadoAtencion.EnAtencion] = "En atención",
        [EstadoAtencion.Atendido] = "Atendido",
        [EstadoAtencion.Finalizada] = "Finalizada",
    };

    private static readonly Dictionary<string, string> Cierres = new()
    {
        ["DEVUELTA"] = "Tarjeta devuelta",
        ["SIN_LECTURAS"] = "Sin lecturas",
        ["CIERRE_JORNADA"] = "Cierre de jornada",
    };

    public static byte[] Generar(IEnumerable<FilaHistorial> filas, ZonaHoraria zona)
    {
        var sb = new StringBuilder();
        Linea(sb, Encabezado);
        foreach (var f in filas)
        {
            Linea(sb,
            [
                f.Codigo,
                f.Fecha.ToString("yyyy-MM-dd"),
                zona.ALocal(f.InicioEn).ToString("HH:mm"),
                f.FinEn is { } fin ? zona.ALocal(fin).ToString("HH:mm") : "",
                Estados.GetValueOrDefault(f.Estado, f.Estado),
                f.Motivo is null ? "" : Cierres.GetValueOrDefault(f.Motivo, f.Motivo),
                f.EsperaMin.ToString(),
                f.ConsultaMin?.ToString() ?? "",
                f.TotalMin.ToString(),
            ]);
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static void Linea(StringBuilder sb, IEnumerable<string> campos) =>
        sb.Append(string.Join(',', campos.Select(Campo))).Append("\r\n");

    /// <summary>Escritura segura de un campo (ver CsvSeguro).</summary>
    public static string Campo(string valor) => CsvSeguro.Campo(valor);
}
