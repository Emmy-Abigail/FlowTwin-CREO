using System.Linq.Expressions;
using FlowTwin.Atenciones.Application;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Auditoria.Application;
using FlowTwin.Auditoria.Domain;
using FlowTwin.Dispositivos.Application;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Application;
using FlowTwin.Gemelo.Domain;
using FlowTwin.Identidad.Application;
using FlowTwin.Identidad.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FlowTwin.Persistence;

/// <summary>
/// Un solo DbContext para todos los módulos, con las tablas separadas por esquema
/// (sección "Capas dentro de cada módulo"): una sola cadena de migraciones y transacciones simples.
/// Cada módulo lo ve solo a través de su propia interfaz (IIdentidadDb, IAtencionesDb…).
/// </summary>
public sealed class FlowTwinDbContext(DbContextOptions<FlowTwinDbContext> options)
    : DbContext(options), IIdentidadDb, IGemeloDb, IDispositivosDb, IAtencionesDb, IAuditoriaDb
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioPiso> UsuarioPisos => Set<UsuarioPiso>();
    public DbSet<HistorialContrasena> HistorialContrasenas => Set<HistorialContrasena>();
    public DbSet<SesionUsuario> Sesiones => Set<SesionUsuario>();
    public DbSet<Sede> Sedes => Set<Sede>();
    public DbSet<Piso> Pisos => Set<Piso>();
    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<ElementoPlano> ElementosPlano => Set<ElementoPlano>();
    public DbSet<Tarjeta> Tarjetas => Set<Tarjeta>();
    public DbSet<MovimientoTarjeta> MovimientosTarjeta => Set<MovimientoTarjeta>();
    public DbSet<Nodo> Nodos => Set<Nodo>();
    public DbSet<EventoNodo> EventosNodo => Set<EventoNodo>();
    public DbSet<Atencion> Atenciones => Set<Atencion>();
    public DbSet<LecturaBle> LecturasBle => Set<LecturaBle>();
    public DbSet<EventoZona> EventosZona => Set<EventoZona>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    public Task<int> GuardarAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    public async Task<T> EnTransaccionAsync<T>(Func<CancellationToken, Task<T>> trabajo, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null) return await trabajo(ct);
        await using var transaccion = await Database.BeginTransactionAsync(ct);
        var resultado = await trabajo(ct);
        await transaccion.CommitAsync(ct);
        return resultado;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configuracion)
    {
        // Todas las fechas son DATETIME2(3) en UTC (sección "Diccionario de datos", ADR-14).
        // Al leerlas se marcan como UTC para que la API las envíe con "Z" y el navegador no las confunda.
        configuracion.Properties<DateTime>().HavePrecision(3).HaveConversion<UtcConverter>();
        configuracion.Properties<DateTime?>().HavePrecision(3).HaveConversion<UtcNullableConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(FlowTwinDbContext).Assembly);
        ConvencionRegistro(modelo);
    }

    /// <summary>
    /// Columnas de registro en todas las tablas (ADR-18): FechaRegistro con default SYSUTCDATETIME(),
    /// EstadoRegistro con default 1 y un filtro global que oculta los dados de baja (RN-29).
    /// Para verlos: IgnoreQueryFilters().
    /// </summary>
    private static void ConvencionRegistro(ModelBuilder modelo)
    {
        foreach (var tipo in modelo.Model.GetEntityTypes())
        {
            var clr = tipo.ClrType;
            if (typeof(IRegistroCreacion).IsAssignableFrom(clr))
            {
                modelo.Entity(clr).Property(nameof(IRegistroCreacion.FechaRegistro)).HasDefaultValueSql("SYSUTCDATETIME()");
                modelo.Entity(clr).Property(nameof(IRegistroCreacion.UsuarioRegistroId)).HasDefaultValue(UsuarioSistema.Id);
            }
            if (typeof(IEstadoRegistro).IsAssignableFrom(clr))
            {
                modelo.Entity(clr).Property(nameof(IEstadoRegistro.EstadoRegistro)).HasDefaultValue(true);
                var e = Expression.Parameter(clr, "e");
                var filtro = Expression.Lambda(Expression.Property(e, nameof(IEstadoRegistro.EstadoRegistro)), e);
                modelo.Entity(clr).HasQueryFilter(filtro);
            }
            if (typeof(EntidadEditable).IsAssignableFrom(clr))
                modelo.Entity(clr).Property(nameof(EntidadEditable.Version)).IsRowVersion();
        }
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class UtcNullableConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
