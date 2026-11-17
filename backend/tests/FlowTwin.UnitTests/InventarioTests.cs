using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>P9 · Dispositivos: alta de tarjetas, última detección y qué «requiere atención».</summary>
public class InventarioTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Una_tarjeta_nueva_trae_su_codigo_su_QR_y_su_identificador_BLE()
    {
        var t = Tarjeta.Registrar(16, sedeId: 1);

        Assert.Equal(("T-16", "CREO-T16"), (t.Codigo, t.CodigoQr));
        Assert.Equal(EmisorBle.Identificador(major: 1, minor: 16), t.UuidBle);   // major = sede, minor = número
        Assert.Equal("T-1234", Tarjeta.Registrar(1234, 1).Codigo);               // escala más allá de 999
        Assert.Throws<ReglaNegocioException>(() => Tarjeta.Registrar(70_000, 1));
        Assert.Equal("T-1234", Tarjeta.NormalizarCodigo("t1234"));
    }

    [Fact]
    public void Una_deteccion_vieja_no_reemplaza_a_una_mas_nueva()
    {
        var t = Tarjeta.Registrar(1, 1);
        t.RegistrarDeteccion(Ahora, nodoId: 2);
        t.RegistrarDeteccion(Ahora.AddMinutes(-5), nodoId: 3);   // una lectura que llegó tarde

        Assert.Equal((Ahora, (int?)2), (t.UltimaDeteccionEn!.Value, t.UltimoNodoId));
    }

    private static Tarjeta Con(EstadoTarjeta estado, DateTime? detectada, DateTime registrada)
    {
        var t = Tarjeta.Registrar(1, 1);
        (t.EstadoTarjetaId, t.UltimaDeteccionEn, t.FechaRegistro) = (estado, detectada, registrada);
        return t;
    }

    public static TheoryData<EstadoTarjeta, int?, int, string?> Casos() => new()
    {
        // estado, detectada hace (h), registrada hace (h), motivo esperado
        { EstadoTarjeta.Disponible, 2, 100, null },                                     // se ve a diario: bien
        { EstadoTarjeta.Disponible, 30, 100, ReglasInventario.SinDeteccionReciente },   // ¿batería?
        { EstadoTarjeta.PorDesinfectar, 30, 100, ReglasInventario.SinDeteccionReciente },
        { EstadoTarjeta.Disponible, null, 30, ReglasInventario.NuncaDetectada },
        { EstadoTarjeta.Disponible, null, 2, null },                                    // recién registrada: aún no
        { EstadoTarjeta.Extraviada, null, 100, ReglasInventario.Extraviada },
        { EstadoTarjeta.EnUso, 30, 100, null },                                         // la vigila el Operador (alertas)
        { EstadoTarjeta.SinDevolver, 30, 100, null },                                   // la atiende Admisión: no se duplica
        { EstadoTarjeta.Baja, null, 100, null },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Requiere_atencion_lo_mismo_en_la_consulta_y_en_el_motivo(EstadoTarjeta estado, int? detectadaHace, int registradaHace, string? motivo)
    {
        var t = Con(estado, detectadaHace is null ? null : Ahora.AddHours(-detectadaHace.Value), Ahora.AddHours(-registradaHace));

        var enConsulta = ReglasInventario.TarjetaRequiereAtencion(Ahora).Compile()(t);

        Assert.Equal(motivo, ReglasInventario.MotivoTarjeta(t.EstadoTarjetaId, t.UltimaDeteccionEn, t.FechaRegistro, Ahora));
        Assert.Equal(motivo is not null, enConsulta);   // la regla de SQL y la del motivo no pueden desalinearse
    }
}
