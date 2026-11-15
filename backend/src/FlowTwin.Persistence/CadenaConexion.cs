using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace FlowTwin.Persistence;

/// <summary>
/// Arma la cadena de conexión final. La cadena (con su contraseña) vive en user-secrets o en variables
/// de entorno; «BaseDatos:Nombre» solo cambia la base, para levantar una segunda API sobre otra base
/// sin copiar el secreto. Lo usa el perfil «simulador»: el simulador de nodos escribe en
/// FlowTwin_Simulador y nunca se mezcla con los datos de la base de trabajo.
/// </summary>
public static partial class CadenaConexion
{
    public static string ConBase(string cadena, string? nombreBase)
    {
        if (string.IsNullOrWhiteSpace(nombreBase)) return cadena;
        if (!NombreValido().IsMatch(nombreBase))
            throw new InvalidOperationException(
                $"BaseDatos:Nombre «{nombreBase}» no es válido: usa solo letras, números y guion bajo (máx. 128).");
        return new SqlConnectionStringBuilder(cadena) { InitialCatalog = nombreBase }.ConnectionString;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,127}$")]
    private static partial Regex NombreValido();
}
