using FlowTwin.Atenciones.Domain;
using FlowTwin.Auditoria.Domain;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Domain;
using FlowTwin.Identidad.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowTwin.Persistence.Configuraciones;

/// <summary>Esquema ope. EventoZona, PausaZona y Alerta llegan en los siguientes pasos del Avance 2.</summary>
public sealed class AtencionConfig : IEntityTypeConfiguration<Atencion>
{
    public void Configure(EntityTypeBuilder<Atencion> b)
    {
        b.ToTable("Atencion", "ope", t =>
        {
            t.HasCheckConstraint("CK_Atencion_Fin", "[FinEn] IS NULL OR [FinEn] >= [InicioEn]");
            // El motivo se llena junto con FinEn, nunca uno sin el otro
            t.HasCheckConstraint("CK_Atencion_Motivo", "([FinEn] IS NULL AND [MotivoCierreId] IS NULL) OR ([FinEn] IS NOT NULL AND [MotivoCierreId] IS NOT NULL)");
        });
        b.Property(a => a.Codigo).HasColumnType("varchar(10)").IsRequired();
        b.HasIndex(a => new { a.SedeId, a.FechaAtencion, a.Codigo }).IsUnique();          // RN-02
        b.HasIndex(a => a.TarjetaId).IsUnique().HasFilter("[FinEn] IS NULL")                // RN-01
            .HasDatabaseName("UX_Atencion_TarjetaAbierta");
        b.HasIndex(a => new { a.SedeId, a.InicioEn });
        b.HasOne<Sede>().WithMany().HasForeignKey(a => a.SedeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Tarjeta>().WithMany().HasForeignKey(a => a.TarjetaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<MotivoCierreAtencion>>().WithMany().HasForeignKey(a => a.MotivoCierreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.RecibidaPor).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(a => a.EstaAbierta);
    }
}

/// <summary>Esquema aud: solo inserciones (RN-27).</summary>
public sealed class AuditoriaConfig : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> b)
    {
        b.ToTable("Auditoria", "aud", t =>
        {
            t.HasCheckConstraint("CK_Auditoria_Antes", "[ValorAntes] IS NULL OR ISJSON([ValorAntes]) = 1");
            t.HasCheckConstraint("CK_Auditoria_Despues", "[ValorDespues] IS NULL OR ISJSON([ValorDespues]) = 1");
        });
        b.Property(a => a.Entidad).HasColumnType("varchar(60)").IsRequired();
        b.Property(a => a.EntidadId).HasColumnType("varchar(40)");
        b.Property(a => a.Ip).HasColumnType("varchar(45)");
        b.Property(a => a.TraceId).HasColumnType("varchar(64)");
        b.HasOne<Catalogo<AccionAuditoria>>().WithMany().HasForeignKey(a => a.AccionAuditoriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => a.FechaRegistro);
        b.HasIndex(a => new { a.UsuarioRegistroId, a.FechaRegistro });
    }
}

/// <summary>
/// ope.LecturaBle: la tabla de más volumen (~1 millón de filas por jornada, ver PurgaLecturasServicio).
/// Índice por LeidaEn para el motor y la purga; por (UuidBle, LeidaEn) para seguir un emisor y detectar
/// los no registrados (F-16).
/// </summary>
public sealed class LecturaBleConfig : IEntityTypeConfiguration<LecturaBle>
{
    public void Configure(EntityTypeBuilder<LecturaBle> b)
    {
        b.ToTable("LecturaBle", "ope", t => t.HasCheckConstraint("CK_LecturaBle_Rssi", "[Rssi] BETWEEN -127 AND 0"));
        b.Property(l => l.UuidBle).HasColumnType("varchar(64)").IsRequired();
        b.HasIndex(l => l.LeidaEn);
        b.HasIndex(l => new { l.UuidBle, l.LeidaEn });
        b.HasOne<Nodo>().WithMany().HasForeignKey(l => l.NodoId).OnDelete(DeleteBehavior.Restrict);
    }
}
