namespace FlowTwin.SharedKernel;

/// <summary>
/// Columnas de registro que pidió el patrocinador (ADR-18): cuándo y quién creó la fila.
/// Las llena solo el RegistroInterceptor de Persistence; el código de negocio no las toca.
/// </summary>
public interface IRegistroCreacion
{
    DateTime FechaRegistro { get; set; }
    int UsuarioRegistroId { get; set; }
}

/// <summary>Además, cuándo y quién hizo el último cambio (incluida la baja).</summary>
public interface IRegistroActualizacion : IRegistroCreacion
{
    DateTime? FechaActualizacion { get; set; }
    int? UsuarioActualizacionId { get; set; }
}

/// <summary>Borrado lógico (RN-28): 1 = activo, 0 = dado de baja. Nunca se borra la fila.</summary>
public interface IEstadoRegistro
{
    bool EstadoRegistro { get; set; }
}

/// <summary>
/// Base de los eventos e historiales (EventoZona, MovimientoTarjeta, Auditoria…):
/// solo se insertan, nunca se editan ni se dan de baja.
/// </summary>
public abstract class EntidadEvento : IRegistroCreacion
{
    public DateTime FechaRegistro { get; set; }
    public int UsuarioRegistroId { get; set; }
}

/// <summary>Base de las tablas que se editan pero no se dan de baja (por ejemplo, Atencion).</summary>
public abstract class EntidadConActualizacion : IRegistroActualizacion
{
    public DateTime FechaRegistro { get; set; }
    public int UsuarioRegistroId { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public int? UsuarioActualizacionId { get; set; }
}

/// <summary>
/// Base de las tablas editables (Usuario, Sede, Piso, Tarjeta, catálogos…):
/// columnas de registro, EstadoRegistro y control de concurrencia (rowversion → 409).
/// </summary>
public abstract class EntidadEditable : EntidadConActualizacion, IEstadoRegistro
{
    public bool EstadoRegistro { get; set; } = true;
    public byte[] Version { get; set; } = [];
}
