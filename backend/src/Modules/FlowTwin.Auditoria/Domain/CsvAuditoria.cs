using System.Text;
using FlowTwin.SharedKernel;

namespace FlowTwin.Auditoria.Domain;

/// <summary>El CSV de la bitácora: horas de Lima, BOM UTF-8 para Excel y campos a prueba de fórmulas (CsvSeguro).</summary>
public static class CsvAuditoria
{
    private static readonly string[] Encabezado = ["Fecha y hora", "Usuario", "Acción", "Categoría", "Entidad", "Objeto", "IP"];

    public static readonly IReadOnlyDictionary<string, string> NombreDeCategoria = new Dictionary<string, string>
    {
        [CategoriasAuditoria.Accesos] = "Accesos", [CategoriasAuditoria.Usuarios] = "Usuarios",
        [CategoriasAuditoria.Configuracion] = "Configuración", [CategoriasAuditoria.Dispositivos] = "Dispositivos",
        [CategoriasAuditoria.Operacion] = "Operación",
    };

    public static byte[] Generar(IEnumerable<FilaExportada> filas, ZonaHoraria zona)
    {
        var sb = new StringBuilder();
        Linea(sb, Encabezado);
        foreach (var f in filas)
            Linea(sb, [zona.ALocal(f.Cuando).ToString("yyyy-MM-dd HH:mm:ss"), f.Usuario, f.AccionTexto,
                NombreDeCategoria.GetValueOrDefault(f.Categoria, f.Categoria), f.Entidad, f.Objeto ?? "", f.Ip ?? ""]);
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static void Linea(StringBuilder sb, IEnumerable<string> campos) =>
        sb.Append(string.Join(',', campos.Select(CsvSeguro.Campo))).Append("\r\n");
}

public sealed record FilaExportada(DateTime Cuando, string Usuario, string AccionTexto, string Categoria, string Entidad, string? Objeto, string? Ip);
