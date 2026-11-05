using FlowTwin.Gemelo.Domain;
using FlowTwin.Identidad.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowTwin.Persistence.Configuraciones;

/// <summary>Esquema seg (sección Diccionario de datos → Esquema seg).</summary>
public sealed class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuario", "seg", t => t.HasCheckConstraint("CK_Usuario_IntentosFallidos", "[IntentosFallidos] >= 0"));
        b.Property(u => u.Correo).HasMaxLength(120).IsRequired();
        b.HasIndex(u => u.Correo).IsUnique();
        b.Property(u => u.Nombres).HasMaxLength(80).IsRequired();
        b.Property(u => u.Apellidos).HasMaxLength(80).IsRequired();
        b.Property(u => u.ContrasenaHash).HasMaxLength(200).IsRequired();
        // Sin default en la base: con HasDefaultValue(true), EF trataría false como "no indicado"
        // y SQL Server guardaría true. El valor inicial (true) lo pone la clase Usuario.
        b.HasOne<Catalogo<Rol>>().WithMany().HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(u => u.Pisos).WithOne().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(u => u.NombreCompleto);

        // ADR-19: el usuario Sistema firma las acciones automáticas. No puede iniciar sesión:
        // su hash "!" nunca coincide con ninguna contraseña.
        b.HasData(new
        {
            Id = UsuarioSistema.Id,
            Correo = "sistema@flowtwin.local",
            Nombres = "Sistema",
            Apellidos = "FlowTwin",
            RolId = Rol.Admin,
            ContrasenaHash = "!",
            DebeCambiarContrasena = false,
            IntentosFallidos = (byte)0,
            EstadoRegistro = true,
            FechaRegistro = Semilla.Fecha,
            UsuarioRegistroId = UsuarioSistema.Id,
        });
    }
}

public sealed class UsuarioPisoConfig : IEntityTypeConfiguration<UsuarioPiso>
{
    public void Configure(EntityTypeBuilder<UsuarioPiso> b)
    {
        b.ToTable("UsuarioPiso", "seg");
        b.HasKey(p => new { p.UsuarioId, p.PisoId });
        b.HasOne<Piso>().WithMany().HasForeignKey(p => p.PisoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HistorialContrasenaConfig : IEntityTypeConfiguration<HistorialContrasena>
{
    public void Configure(EntityTypeBuilder<HistorialContrasena> b)
    {
        b.ToTable("HistorialContrasena", "seg");
        b.Property(h => h.ContrasenaHash).HasMaxLength(200).IsRequired();
        b.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.UsuarioId, h.Id });
    }
}

public sealed class SesionUsuarioConfig : IEntityTypeConfiguration<SesionUsuario>
{
    public void Configure(EntityTypeBuilder<SesionUsuario> b)
    {
        b.ToTable("SesionUsuario", "seg", t => t.HasCheckConstraint("CK_SesionUsuario_Expira", "[ExpiraEn] > [FechaRegistro]"));
        b.Property(s => s.TokenRefrescoHash).HasColumnType("binary(32)").IsRequired();
        b.HasIndex(s => s.TokenRefrescoHash).IsUnique();
        b.Property(s => s.Ip).HasColumnType("varchar(45)");
        b.HasOne<Usuario>().WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => new { s.UsuarioId, s.RevocadaEn });
    }
}
