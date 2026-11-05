using FlowTwin.SharedKernel;

namespace FlowTwin.Identidad.Domain;

/// <summary>Catálogo cat.Rol. Cada usuario tiene un solo rol (ADR-12).</summary>
public enum Rol
{
    [Catalogo("ADMIN", "Administrador")] Admin = 1,
    [Catalogo("OPERADOR", "Operador")] Operador = 2,
    [Catalogo("GERENCIA", "Gerencia")] Gerencia = 3,
}

/// <summary>seg.Usuario: personal que usa el sistema. Nunca se borra: se da de baja (RN-22).</summary>
public class Usuario : EntidadEditable
{
    public const int IntentosMaximos = 5;

    public int Id { get; set; }
    public string Correo { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public Rol RolId { get; set; }
    public string ContrasenaHash { get; set; } = "";
    public bool DebeCambiarContrasena { get; set; } = true;
    public DateTime? ContrasenaTemporalExpiraEn { get; set; }
    public DateTime? ContrasenaCambiadaEn { get; set; }
    public byte IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime? UltimoIngresoEn { get; set; }
    public List<UsuarioPiso> Pisos { get; set; } = [];

    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();

    public bool EstaBloqueado(DateTime ahora) => BloqueadoHasta > ahora;

    /// <summary>F-01: 5 intentos fallidos seguidos bloquean la cuenta.</summary>
    public void RegistrarIntentoFallido(DateTime ahora, TimeSpan duracionBloqueo)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= IntentosMaximos)
        {
            BloqueadoHasta = ahora + duracionBloqueo;
            IntentosFallidos = 0;
        }
    }

    public void RegistrarIngreso(DateTime ahora)
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        UltimoIngresoEn = ahora;
    }

    public void CambiarContrasena(string nuevoHash, DateTime ahora)
    {
        ContrasenaHash = nuevoHash;
        ContrasenaCambiadaEn = ahora;
        DebeCambiarContrasena = false;
        ContrasenaTemporalExpiraEn = null;
    }

    /// <summary>F-04 y F-05: contraseña temporal que obliga a cambiarla en el siguiente ingreso.</summary>
    public void AsignarTemporal(string hash, DateTime expiraEn)
    {
        ContrasenaHash = hash;
        DebeCambiarContrasena = true;
        ContrasenaTemporalExpiraEn = expiraEn;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }
}

/// <summary>seg.UsuarioPiso: pisos que ve cada usuario (RN-24).</summary>
public class UsuarioPiso : EntidadEditable
{
    public int UsuarioId { get; set; }
    public int PisoId { get; set; }
}

/// <summary>seg.HistorialContrasena: hashes anteriores, para no repetir las 3 últimas.</summary>
public class HistorialContrasena : EntidadEvento
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string ContrasenaHash { get; set; } = "";
}

/// <summary>
/// seg.SesionUsuario: una sesión abierta, identificada por el hash SHA-256 de su token de refresco
/// (el token en sí nunca se guarda). FechaRegistro hace de "creada en".
/// </summary>
public class SesionUsuario : EntidadConActualizacion
{
    public long Id { get; set; }
    public int UsuarioId { get; set; }
    public byte[] TokenRefrescoHash { get; set; } = [];
    public DateTime UltimaActividadEn { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? RevocadaEn { get; set; }
    public string? Ip { get; set; }

    public bool EstaVigente(DateTime ahora, TimeSpan inactividadMaxima) =>
        RevocadaEn is null && ExpiraEn > ahora && ahora - UltimaActividadEn <= inactividadMaxima;

    public void Revocar(DateTime ahora) => RevocadaEn ??= ahora;
}
