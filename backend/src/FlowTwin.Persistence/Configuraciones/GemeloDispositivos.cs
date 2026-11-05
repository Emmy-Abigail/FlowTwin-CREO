using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowTwin.Persistence.Configuraciones;

/// <summary>Esquema gem. Zona, ElementoPlano y PublicacionPiso llegan en el Avance 2.</summary>
public sealed class SedeConfig : IEntityTypeConfiguration<Sede>
{
    public void Configure(EntityTypeBuilder<Sede> b)
    {
        b.ToTable("Sede", "gem");
        b.Property(s => s.Nombre).HasMaxLength(100).IsRequired();
        b.HasIndex(s => s.Nombre).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.Property(s => s.Direccion).HasMaxLength(200);
        b.Property(s => s.PrefijoAtencion).HasColumnType("char(1)").HasDefaultValue('A');
        b.HasData(new
        {
            Id = 1, Nombre = "Sede Principal", Direccion = "Jr. Eloy Espinoza 700, San Martín de Porres",
            PrefijoAtencion = 'A', EstadoRegistro = true, FechaRegistro = Semilla.Fecha, UsuarioRegistroId = UsuarioSistema.Id,
        });
    }
}

public sealed class PisoConfig : IEntityTypeConfiguration<Piso>
{
    public void Configure(EntityTypeBuilder<Piso> b)
    {
        b.ToTable("Piso", "gem", t =>
        {
            t.HasCheckConstraint("CK_Piso_AnchoM", "[AnchoM] > 0");
            t.HasCheckConstraint("CK_Piso_FondoM", "[FondoM] > 0");
        });
        b.Property(p => p.Nombre).HasMaxLength(60).IsRequired();
        b.HasIndex(p => new { p.SedeId, p.Nombre }).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.HasIndex(p => new { p.SedeId, p.Nivel }).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.Property(p => p.PlanoRuta).HasMaxLength(260);
        b.Property(p => p.AnchoM).HasPrecision(6, 2);
        b.Property(p => p.FondoM).HasPrecision(6, 2);
        b.Property(p => p.AltoMuroM).HasPrecision(4, 2).HasDefaultValue(2.70m);
        b.HasOne<Sede>().WithMany().HasForeignKey(p => p.SedeId).OnDelete(DeleteBehavior.Restrict);
        // Piso 1: geometría real de la lámina A-07 de AutoCAD (40.12 × 20.99 m)
        b.HasData(new
        {
            Id = 1, SedeId = 1, Nombre = "Piso 1", Nivel = (short)1, PlanoRuta = "planos/piso1.jpg",
            AnchoM = (decimal?)40.12m, FondoM = (decimal?)20.99m, AltoMuroM = 2.70m, Publicado = true,
            PublicadoEn = (DateTime?)Semilla.Fecha,
            EstadoRegistro = true, FechaRegistro = Semilla.Fecha, UsuarioRegistroId = UsuarioSistema.Id,
        });
    }
}

/// <summary>Esquema dis. Nodo y EventoNodo llegan en el Avance 2.</summary>
public sealed class TarjetaConfig : IEntityTypeConfiguration<Tarjeta>
{
    public void Configure(EntityTypeBuilder<Tarjeta> b)
    {
        b.ToTable("Tarjeta", "dis");
        b.Property(t => t.Codigo).HasColumnType("varchar(10)").IsRequired();
        b.Property(t => t.UuidBle).HasColumnType("varchar(64)").IsRequired();
        b.Property(t => t.CodigoQr).HasColumnType("varchar(64)").IsRequired();
        // Únicos solo entre registros activos: un código dado de baja se puede volver a usar (RN-28)
        b.HasIndex(t => t.Codigo).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.HasIndex(t => t.UuidBle).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.HasIndex(t => t.CodigoQr).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.HasOne<Sede>().WithMany().HasForeignKey(t => t.SedeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<EstadoTarjeta>>().WithMany().HasForeignKey(t => t.EstadoTarjetaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MovimientoTarjetaConfig : IEntityTypeConfiguration<MovimientoTarjeta>
{
    public void Configure(EntityTypeBuilder<MovimientoTarjeta> b)
    {
        b.ToTable("MovimientoTarjeta", "dis");
        b.Property(m => m.Observacion).HasMaxLength(200);
        b.HasOne<Tarjeta>().WithMany().HasForeignKey(m => m.TarjetaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<EstadoTarjeta>>().WithMany().HasForeignKey(m => m.EstadoAnteriorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<EstadoTarjeta>>().WithMany().HasForeignKey(m => m.EstadoNuevoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Atencion>().WithMany().HasForeignKey(m => m.AtencionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(m => new { m.TarjetaId, m.Id });
    }
}
