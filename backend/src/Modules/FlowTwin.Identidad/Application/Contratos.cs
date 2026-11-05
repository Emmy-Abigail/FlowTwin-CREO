using FlowTwin.Identidad.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Identidad.Application;

/// <summary>
/// Las tablas que usa Identidad. La implementa el FlowTwinDbContext de Persistence:
/// el módulo no sabe que detrás hay SQL Server (puerto de la arquitectura limpia).
/// </summary>
public interface IIdentidadDb : IUnidadDeTrabajo
{
    DbSet<Usuario> Usuarios { get; }
    DbSet<UsuarioPiso> UsuarioPisos { get; }
    DbSet<HistorialContrasena> HistorialContrasenas { get; }
    DbSet<SesionUsuario> Sesiones { get; }
}

/// <summary>Firma el token de acceso (JWT). La implementa la Api, que es la que conoce la librería JWT.</summary>
public interface IEmisorTokens
{
    (string Token, DateTime ExpiraEn) Emitir(Usuario usuario, IReadOnlyList<int> pisos, bool debeCambiar);
}

/// <summary>Sección "Jwt" de appsettings.json.</summary>
public sealed class OpcionesJwt
{
    public string Emisor { get; set; } = "flowtwin";
    /// <summary>Clave HMAC de al menos 32 bytes. Va en user-secrets o en una variable de entorno, nunca en el código.</summary>
    public string Clave { get; set; } = "";
    public int MinutosAcceso { get; set; } = 15;
    public int MinutosInactividad { get; set; } = 30;
    /// <summary>Duración máxima de una sesión aunque haya actividad (un turno).</summary>
    public int HorasSesion { get; set; } = 12;
}

/// <summary>Sección "Contrasenas" de appsettings.json.</summary>
public sealed class OpcionesContrasenas
{
    public int HistorialNoRepetir { get; set; } = 3;
    public int MinutosBloqueo { get; set; } = 15;
    public int HorasTemporal { get; set; } = 24;
    /// <summary>
    /// F-02: obliga a cambiar la contraseña temporal en el primer ingreso. Por defecto se exige;
    /// el patrocinador pidió desactivarlo por ahora (appsettings.json). El dato se sigue guardando.
    /// </summary>
    public bool ExigirCambioPrimerIngreso { get; set; } = true;
}

/// <summary>Lo que el frontend sabe del usuario con sesión.</summary>
public sealed record UsuarioDto(
    int Id, string Correo, string Nombres, string Apellidos, string Rol, int[] Pisos, bool DebeCambiarContrasena)
{
    /// <param name="debeCambiar">Si el usuario debe cambiar su contraseña ahora (ya considera si la regla está activa).</param>
    public static UsuarioDto De(Usuario u, bool debeCambiar) => new(
        u.Id, u.Correo, u.Nombres, u.Apellidos, u.RolId.Codigo(),
        [.. u.Pisos.Where(p => p.EstadoRegistro).Select(p => p.PisoId).Order()], debeCambiar);
}

/// <summary>Resultado de iniciar o renovar sesión. El token de refresco viaja solo en la cookie.</summary>
public sealed record SesionIniciada(
    string AccessToken, DateTime ExpiraEn, UsuarioDto Usuario, string TokenRefresco, DateTime RefrescoExpiraEn);
