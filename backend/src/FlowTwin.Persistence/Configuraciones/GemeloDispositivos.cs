using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowTwin.Persistence.Configuraciones;

/// <summary>Esquema gem. PublicacionPiso llega en un paso siguiente del Avance 2.</summary>
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

public sealed class ZonaConfig : IEntityTypeConfiguration<Zona>
{
    public void Configure(EntityTypeBuilder<Zona> b)
    {
        // Los tipos con aforo, en SQL, salen del mismo enum que usa el dominio: no hay números mágicos
        var conAforo = string.Join(", ", Enum.GetValues<TipoZona>().Where(t => t.TieneAforo()).Select(t => (int)t));
        var sinAforo = string.Join(", ", Enum.GetValues<TipoZona>().Where(t => !t.TieneAforo()).Select(t => (int)t));

        b.ToTable("Zona", "gem", t =>
        {
            t.HasCheckConstraint("CK_Zona_Posicion", "[X] >= 0 AND [Y] >= 0");
            t.HasCheckConstraint("CK_Zona_Tamano", "[AnchoM] > 0 AND [FondoM] > 0");
            t.HasCheckConstraint("CK_Zona_Geometria", "[Geometria] IS NULL OR ISJSON([Geometria]) = 1");
            // RN-13 también en la base: la protege aunque alguien inserte filas por fuera de la API
            t.HasCheckConstraint("CK_Zona_AforoRN13",
                $"([TipoZonaId] IN ({conAforo}) AND [Aforo] >= 1 AND [UmbralMediaPct] > 0 " +
                "AND [UmbralMediaPct] <= [UmbralAltaPct] AND [UmbralAltaPct] <= 100) " +
                $"OR ([TipoZonaId] IN ({sinAforo}) AND [Aforo] IS NULL AND [UmbralMediaPct] IS NULL AND [UmbralAltaPct] IS NULL)");
        });
        b.Property(z => z.Nombre).HasMaxLength(80).IsRequired();
        b.HasIndex(z => new { z.PisoId, z.Nombre }).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.Property(z => z.X).HasPrecision(6, 2);
        b.Property(z => z.Y).HasPrecision(6, 2);
        b.Property(z => z.AnchoM).HasPrecision(6, 2);
        b.Property(z => z.FondoM).HasPrecision(6, 2);
        b.HasOne<Piso>().WithMany().HasForeignKey(z => z.PisoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<TipoZona>>().WithMany().HasForeignKey(z => z.TipoZonaId).OnDelete(DeleteBehavior.Restrict);

        // Las 8 zonas del Piso 1 medidas sobre la lámina A-07 (antes en public/pisos/geometria-piso1.json).
        // El Hall es Tránsito: sin aforo ni umbrales (RN-13).
        b.HasData(
            ZonaPiso1(1, TipoZona.Ingreso, "Admisión y caja", 35.0m, 8.7m, 2.1m, 5.1m, 6),
            ZonaPiso1(2, TipoZona.Transito, "Hall principal", 28.5m, 10.2m, 6.2m, 4.8m, null),
            ZonaPiso1(3, TipoZona.Espera, "Sala de espera", 19.3m, 15.0m, 5.7m, 5.3m, 24),
            ZonaPiso1(4, TipoZona.Servicio, "Triaje", 25.2m, 13.9m, 3.1m, 4.8m, 3),
            ZonaPiso1(5, TipoZona.Servicio, "Tópico de enfermería", 11.2m, 11.9m, 3.1m, 4.7m, 3),
            ZonaPiso1(6, TipoZona.Servicio, "Tópico urgencia", 9.8m, 16.9m, 5.0m, 3.7m, 4),
            ZonaPiso1(7, TipoZona.Servicio, "Módulo 2 (consultorio)", 2.2m, 15.1m, 7.4m, 5.2m, 6),
            ZonaPiso1(8, TipoZona.Servicio, "Observación", 16.6m, 13.9m, 3.2m, 6.5m, 4));
    }

    /// <summary>Umbrales 60 % y 85 % para todas por ahora: pendiente de validar con la dirección médica.</summary>
    private static object ZonaPiso1(int id, TipoZona tipo, string nombre, decimal x, decimal y, decimal ancho, decimal fondo, short? aforo) => new
    {
        Id = id, PisoId = 1, TipoZonaId = tipo, Nombre = nombre, X = x, Y = y, AnchoM = ancho, FondoM = fondo,
        Aforo = aforo,
        UmbralMediaPct = aforo is null ? null : (byte?)60,
        UmbralAltaPct = aforo is null ? null : (byte?)85,
        EnBorrador = false,
        EstadoRegistro = true, FechaRegistro = Semilla.Fecha, UsuarioRegistroId = UsuarioSistema.Id,
    };
}

/// <summary>
/// gem.ElementoPlano. Los muros del Piso 1 NO van con HasData: son ~28 KB y EF los copiaría en el
/// snapshot de cada migración futura. Se cargan una sola vez en la migración Avance2_Muros.
/// </summary>
public sealed class ElementoPlanoConfig : IEntityTypeConfiguration<ElementoPlano>
{
    public void Configure(EntityTypeBuilder<ElementoPlano> b)
    {
        b.ToTable("ElementoPlano", "gem", t =>
        {
            t.HasCheckConstraint("CK_ElementoPlano_AltoM", "[AltoM] IS NULL OR [AltoM] > 0");
            t.HasCheckConstraint("CK_ElementoPlano_Geometria", "[Geometria] IS NULL OR ISJSON([Geometria]) = 1");
            t.HasCheckConstraint("CK_ElementoPlano_NombreAmbiente",
                $"[TipoElementoPlanoId] <> {(int)TipoElementoPlano.Ambiente} OR [Nombre] IS NOT NULL");
        });
        b.Property(e => e.Nombre).HasMaxLength(80);
        b.Property(e => e.X1).HasPrecision(6, 2);
        b.Property(e => e.Y1).HasPrecision(6, 2);
        b.Property(e => e.X2).HasPrecision(6, 2);
        b.Property(e => e.Y2).HasPrecision(6, 2);
        b.Property(e => e.AltoM).HasPrecision(4, 2);
        b.HasOne<Piso>().WithMany().HasForeignKey(e => e.PisoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<TipoElementoPlano>>().WithMany().HasForeignKey(e => e.TipoElementoPlanoId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Esquema dis.</summary>
public sealed class NodoConfig : IEntityTypeConfiguration<Nodo>
{
    public void Configure(EntityTypeBuilder<Nodo> b)
    {
        b.ToTable("Nodo", "dis", t =>
        {
            // Formato XX:XX:XX:XX:XX:XX en mayúsculas (COLLATE binario: LIKE distinguiría 'a' de 'A')
            var hex = "[0-9A-F][0-9A-F]";
            t.HasCheckConstraint("CK_Nodo_Mac", $"[Mac] COLLATE Latin1_General_BIN2 LIKE '{string.Join(":", Enumerable.Repeat(hex, 6))}'");
            t.HasCheckConstraint("CK_Nodo_AjusteRssiDb", $"[AjusteRssiDb] BETWEEN -{Nodo.AjusteMaximoDb} AND {Nodo.AjusteMaximoDb}");
        });
        b.Property(n => n.Codigo).HasColumnType("varchar(10)").IsRequired();
        b.Property(n => n.Mac).HasColumnType("char(17)").IsRequired();
        b.Property(n => n.ClaveApiHash).HasColumnType("binary(32)").IsRequired();
        b.Property(n => n.PosX).HasPrecision(6, 2);
        b.Property(n => n.PosY).HasPrecision(6, 2);
        b.Property(n => n.VersionFirmware).HasColumnType("varchar(20)");
        b.Property(n => n.AjusteRssiDb).HasDefaultValue((short)0);
        b.HasIndex(n => n.Codigo).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.HasIndex(n => n.Mac).IsUnique().HasFilter("[EstadoRegistro] = 1");
        b.HasOne<Zona>().WithMany().HasForeignKey(n => n.ZonaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<EstadoNodo>>().WithMany().HasForeignKey(n => n.EstadoNodoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EventoNodoConfig : IEntityTypeConfiguration<EventoNodo>
{
    public void Configure(EntityTypeBuilder<EventoNodo> b)
    {
        b.ToTable("EventoNodo", "dis");
        b.Property(e => e.Detalle).HasMaxLength(200);
        b.HasOne<Nodo>().WithMany().HasForeignKey(e => e.NodoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Catalogo<EstadoNodo>>().WithMany().HasForeignKey(e => e.EstadoNodoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.NodoId, e.Id });
    }
}

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
