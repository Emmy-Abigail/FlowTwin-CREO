using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Simulador;

namespace FlowTwin.UnitTests;

/// <summary>El simulador: modelo de señal y formato del identificador del contrato.</summary>
public class SimuladorTests
{
    [Fact]
    public void Sin_ruido_a_1_metro_la_senal_es_la_del_emisor()
    {
        Assert.Equal(-59, ModeloSenal.Rssi(1.0, ruidoDb: 0, new Random(1)));
    }

    [Fact]
    public void La_senal_se_debilita_con_la_distancia()
    {
        var azar = new Random(1);
        var cerca = ModeloSenal.Rssi(2, 0, azar);
        var lejos = ModeloSenal.Rssi(14, 0, azar);   // ~ de la Sala de espera al nodo de Módulo 2

        Assert.True(cerca > lejos);
        Assert.True(lejos < -80);   // por debajo del mínimo del motor: no compite por la zona
    }

    [Fact]
    public void Muy_cerca_no_se_dispara_por_el_logaritmo()
    {
        Assert.Equal(ModeloSenal.Rssi(0.5, 0, new Random(1)), ModeloSenal.Rssi(0.01, 0, new Random(1)));
    }

    [Fact]
    public void El_simulador_arma_el_identificador_igual_que_el_backend()
    {
        // El simulador repite el formato a propósito (no referencia el backend): esta prueba vigila que coincidan
        var escenario = Escenario.Leer(Path.Combine(AppContext.BaseDirectory, "escenarios", "piloto.json"));

        Assert.Equal(EmisorBle.Identificador(major: 1, minor: 7), escenario.UuidBle(7));
    }
}
