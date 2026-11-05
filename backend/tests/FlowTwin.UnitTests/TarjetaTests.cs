using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>Máquina de estados de la tarjeta BLE (diagrama "Tarjeta BLE" del documento).</summary>
public class TarjetaTests
{
    [Theory]
    [InlineData(EstadoTarjeta.Disponible, EstadoTarjeta.EnUso)]          // entrega
    [InlineData(EstadoTarjeta.EnUso, EstadoTarjeta.Disponible)]          // deshacer (RN-04)
    [InlineData(EstadoTarjeta.EnUso, EstadoTarjeta.PorDesinfectar)]      // devolución
    [InlineData(EstadoTarjeta.EnUso, EstadoTarjeta.SinDevolver)]         // 60 min sin lecturas
    [InlineData(EstadoTarjeta.PorDesinfectar, EstadoTarjeta.Disponible)] // desinfectada
    [InlineData(EstadoTarjeta.SinDevolver, EstadoTarjeta.PorDesinfectar)]// aparece y se escanea
    [InlineData(EstadoTarjeta.SinDevolver, EstadoTarjeta.Extraviada)]
    [InlineData(EstadoTarjeta.Extraviada, EstadoTarjeta.PorDesinfectar)]
    [InlineData(EstadoTarjeta.Disponible, EstadoTarjeta.Baja)]
    public void Transicion_permitida_cambia_el_estado_y_registra_el_movimiento(EstadoTarjeta desde, EstadoTarjeta hacia)
    {
        var tarjeta = new Tarjeta { Id = 7, Codigo = "T-07", EstadoTarjetaId = desde };

        var movimiento = tarjeta.CambiarA(hacia, atencionId: 143);

        Assert.Equal(hacia, tarjeta.EstadoTarjetaId);
        Assert.Equal(desde, movimiento.EstadoAnteriorId);
        Assert.Equal(hacia, movimiento.EstadoNuevoId);
        Assert.Equal(143, movimiento.AtencionId);
    }

    [Theory]
    [InlineData(EstadoTarjeta.Disponible, EstadoTarjeta.PorDesinfectar)] // no se puede devolver lo que no se entregó
    [InlineData(EstadoTarjeta.PorDesinfectar, EstadoTarjeta.EnUso)]      // no se entrega sin desinfectar (RN-03)
    [InlineData(EstadoTarjeta.EnUso, EstadoTarjeta.Baja)]                // no se da de baja en uso
    [InlineData(EstadoTarjeta.Baja, EstadoTarjeta.Disponible)]           // la baja es definitiva
    public void Transicion_no_permitida_se_rechaza_sin_cambiar_nada(EstadoTarjeta desde, EstadoTarjeta hacia)
    {
        var tarjeta = new Tarjeta { Codigo = "T-07", EstadoTarjetaId = desde };

        var error = Assert.Throws<ReglaNegocioException>(() => tarjeta.CambiarA(hacia));

        Assert.Equal("RN_TRANSICION_INVALIDA", error.Codigo);
        Assert.Equal(desde, tarjeta.EstadoTarjetaId);
    }

    [Theory]
    [InlineData("T-07", "T-07")]
    [InlineData("t7", "T-07")]
    [InlineData("7", "T-07")]
    [InlineData(" T 12 ", "T-12")]
    [InlineData("creo-t07-8f2a", "CREO-T07-8F2A")]   // contenido del QR: se busca tal cual
    public void Normalizar_codigo_acepta_las_formas_que_escribe_el_recepcionista(string entrada, string esperado) =>
        Assert.Equal(esperado, Tarjeta.NormalizarCodigo(entrada));
}
