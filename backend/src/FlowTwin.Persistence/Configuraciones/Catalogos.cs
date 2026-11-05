using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Domain;
using FlowTwin.Identidad.Domain;
using FlowTwin.Monitoreo.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowTwin.Persistence.Configuraciones;

/// <summary>
/// Los 9 catálogos del esquema cat. Sus filas salen de los enums de C# (Id, Codigo y Nombre),
/// así la base y el código nunca se desalinean.
/// </summary>
public abstract class CatalogoConfig<TEnum>(string tabla) : IEntityTypeConfiguration<Catalogo<TEnum>>
    where TEnum : struct, Enum
{
    public void Configure(EntityTypeBuilder<Catalogo<TEnum>> b)
    {
        b.ToTable(tabla, "cat");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Codigo).HasColumnType("varchar(30)").IsRequired();
        b.Property(c => c.Nombre).HasMaxLength(60).IsRequired();
        b.HasIndex(c => c.Codigo).IsUnique();
        b.HasData(Enum.GetValues<TEnum>().Select(v => new
        {
            Id = v,
            Codigo = v.Codigo(),
            Nombre = v.Nombre(),
            EstadoRegistro = true,
            FechaRegistro = Semilla.Fecha,
            UsuarioRegistroId = UsuarioSistema.Id,
        }));
    }
}

public sealed class RolConfig() : CatalogoConfig<Rol>("Rol");
public sealed class TipoZonaConfig() : CatalogoConfig<TipoZona>("TipoZona");
public sealed class TipoElementoPlanoConfig() : CatalogoConfig<TipoElementoPlano>("TipoElementoPlano");
public sealed class EstadoTarjetaConfig() : CatalogoConfig<EstadoTarjeta>("EstadoTarjeta");
public sealed class EstadoNodoConfig() : CatalogoConfig<EstadoNodo>("EstadoNodo");
public sealed class MotivoCierreAtencionConfig() : CatalogoConfig<MotivoCierreAtencion>("MotivoCierreAtencion");
public sealed class MotivoPausaConfig() : CatalogoConfig<MotivoPausa>("MotivoPausa");
public sealed class TipoAlertaConfig() : CatalogoConfig<TipoAlerta>("TipoAlerta");
public sealed class AccionAuditoriaConfig() : CatalogoConfig<AccionAuditoria>("AccionAuditoria");

/// <summary>Valores fijos de los datos semilla de producción (migración).</summary>
public static class Semilla
{
    public static readonly DateTime Fecha = new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
}
