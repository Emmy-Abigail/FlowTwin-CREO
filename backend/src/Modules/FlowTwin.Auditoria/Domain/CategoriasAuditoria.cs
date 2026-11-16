using System.Linq.Expressions;
using FlowTwin.SharedKernel;

namespace FlowTwin.Auditoria.Domain;

/// <summary>
/// Las categorías con que el Administrador filtra la bitácora (P11). «Operación» (entregas y devoluciones de
/// tarjetas, pausas de zona…) es la mayor parte del volumen: va aparte para que no tape los cambios de
/// configuración, usuarios o accesos. La regla existe dos veces —en C# para rotular y en SQL para filtrar— y una
/// prueba comprueba que dan lo mismo para cada acción y entidad.
/// </summary>
public static class CategoriasAuditoria
{
    public const string Accesos = "ACCESOS", Usuarios = "USUARIOS", Configuracion = "CONFIGURACION",
        Dispositivos = "DISPOSITIVOS", Operacion = "OPERACION";

    public static readonly IReadOnlyList<string> Todas = [Accesos, Usuarios, Configuracion, Dispositivos, Operacion];

    private static readonly AccionAuditoria[] DeAcceso =
        [AccionAuditoria.IniciarSesion, AccionAuditoria.CerrarSesion, AccionAuditoria.CambiarContrasena,
         AccionAuditoria.InicioSesionFallido, AccionAuditoria.CuentaBloqueada];

    private static readonly string[] EntidadesDeConfiguracion = ["Sede", "Piso", "Parametros"];
    private static readonly string[] EntidadesDeDispositivos = ["Nodo", "Tarjeta"];

    public static bool EsValida(string? categoria) => categoria is not null && Todas.Contains(categoria);

    public static string De(AccionAuditoria accion, string entidad)
    {
        if (DeAcceso.Contains(accion)) return Accesos;
        if (entidad == "Usuario") return Usuarios;
        if (EntidadesDeConfiguracion.Contains(entidad) || (entidad == "Zona" && accion is not (AccionAuditoria.Pausar or AccionAuditoria.Reanudar)))
            return Configuracion;
        if (EntidadesDeDispositivos.Contains(entidad)) return Dispositivos;
        return Operacion;
    }

    /// <summary>La misma regla como expresión que EF traduce a SQL.</summary>
    public static Expression<Func<RegistroAuditoria, bool>> Predicado(string categoria)
    {
        var acceso = DeAcceso;
        var config = EntidadesDeConfiguracion;
        var dispositivos = EntidadesDeDispositivos;
        return categoria switch
        {
            Accesos => a => acceso.Contains(a.AccionAuditoriaId),
            Usuarios => a => !acceso.Contains(a.AccionAuditoriaId) && a.Entidad == "Usuario",
            Configuracion => a => !acceso.Contains(a.AccionAuditoriaId) && a.Entidad != "Usuario"
                && (config.Contains(a.Entidad)
                    || (a.Entidad == "Zona" && a.AccionAuditoriaId != AccionAuditoria.Pausar && a.AccionAuditoriaId != AccionAuditoria.Reanudar)),
            Dispositivos => a => !acceso.Contains(a.AccionAuditoriaId) && a.Entidad != "Usuario" && dispositivos.Contains(a.Entidad),
            Operacion => a => !acceso.Contains(a.AccionAuditoriaId) && a.Entidad != "Usuario" && !config.Contains(a.Entidad)
                && !dispositivos.Contains(a.Entidad)
                && !(a.Entidad == "Zona" && a.AccionAuditoriaId != AccionAuditoria.Pausar && a.AccionAuditoriaId != AccionAuditoria.Reanudar),
            _ => throw new ArgumentOutOfRangeException(nameof(categoria), categoria, "Categoría de auditoría desconocida."),
        };
    }
}
