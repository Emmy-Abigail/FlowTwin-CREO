namespace FlowTwin.SharedKernel;

/// <summary>Usuario que firma las acciones automáticas: motor de presencia, cierres, semillas (ADR-19).</summary>
public static class UsuarioSistema
{
    public const int Id = 1;
}

/// <summary>Quién hace la petición actual. Sin sesión (procesos en segundo plano) es el usuario Sistema.</summary>
public interface IUsuarioActual
{
    int Id { get; }
    bool EstaAutenticado { get; }
    string? Rol { get; }
    IReadOnlyList<int> Pisos { get; }
    string? Ip { get; }
    string? TraceId { get; }

    /// <summary>
    /// Durante el login todavía no hay token: este método fija quién firma lo que se guarde
    /// en esta petición (la sesión nueva, su fila de auditoría).
    /// </summary>
    void IdentificarComo(int usuarioId);
}

/// <summary>Hora del servidor, siempre en UTC (ADR-14). Se inyecta para poder fijarla en las pruebas.</summary>
public interface IReloj
{
    DateTime AhoraUtc { get; }
}

public sealed class RelojSistema : IReloj
{
    public DateTime AhoraUtc => DateTime.UtcNow;
}

/// <summary>
/// Zona horaria de la clínica ("ZonaHoraria" en appsettings, America/Lima). La base guarda UTC;
/// solo se convierte para lo que depende del día local, como el correlativo de atenciones (RN-02).
/// </summary>
public sealed class ZonaHoraria(string id)
{
    private readonly TimeZoneInfo zona = TimeZoneInfo.FindSystemTimeZoneById(id);

    public DateTime ALocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona);

    public DateOnly FechaLocal(DateTime utc) => DateOnly.FromDateTime(ALocal(utc));
}

/// <summary>
/// Registra una fila en aud.Auditoria (RN-27) dentro de la MISMA transacción del cambio:
/// la fila se agrega al contexto y se guarda junto con lo demás en un solo SaveChanges.
/// Es transversal, por eso vive aquí; la implementa el módulo Auditoria.
/// </summary>
public interface IAuditoria
{
    void Registrar(AccionAuditoria accion, string entidad, string? entidadId, object? antes = null, object? despues = null);
}

/// <summary>Guarda en una sola transacción todo lo que los casos de uso agregaron o cambiaron.</summary>
public interface IUnidadDeTrabajo
{
    Task<int> GuardarAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta varios GuardarAsync dentro de una sola transacción: o se guarda todo o nada.
    /// Se usa cuando un paso necesita el Id que genera el anterior (la atención y luego el movimiento de su tarjeta).
    /// </summary>
    Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> trabajo, CancellationToken ct = default);
}
