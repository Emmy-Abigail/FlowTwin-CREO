using FlowTwin.Mensajeria.Application;
using FlowTwin.Mensajeria.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>MQTT (ADR-24): temas, cuentas, límite por nodo y qué se le contesta al nodo ante cada error.</summary>
public class MensajeriaTests
{
    private readonly TemasMqtt temas = new("flowtwin");

    [Fact]
    public void Los_temas_y_la_cuenta_de_un_nodo_salen_de_su_MAC()
    {
        var c = temas.CuentaDe("A4:CF:12:9B:3E:03");

        Assert.Equal("flowtwin-a4cf129b3e03", c.Usuario);
        Assert.Equal("flowtwin/v1/nodos/a4cf129b3e03/lecturas", c.TemaLecturas);
        Assert.Equal("flowtwin/v1/nodos/a4cf129b3e03/latidos", c.TemaLatidos);
        Assert.Equal("flowtwin/v1/nodos/a4cf129b3e03/respuestas", c.TemaRespuestas);
        Assert.Equal("flowtwin/v1/nodos/+/lecturas", temas.Filtro(TemasMqtt.Lecturas));
    }

    [Fact]
    public void De_un_tema_recibido_se_obtienen_la_MAC_y_el_tipo()
    {
        Assert.Equal(("A4:CF:12:9B:3E:03", "lecturas"), temas.Leer("flowtwin/v1/nodos/a4cf129b3e03/lecturas"));
    }

    [Theory]
    [InlineData("flowtwin-sim/v1/nodos/a4cf129b3e03/lecturas")]   // de otro entorno
    [InlineData("flowtwin/v1/nodos/a4cf129b3e0/lecturas")]        // MAC incompleta
    [InlineData("flowtwin/v1/nodos/A4CF129B3E03/lecturas")]       // mayúsculas: el contrato dice minúsculas
    [InlineData("flowtwin/v1/nodos/a4cf129b3e03/extra/lecturas")] // más niveles
    [InlineData("otra/cosa")]
    public void Un_tema_que_no_es_de_este_entorno_o_no_tiene_la_forma_se_ignora(string tema)
    {
        Assert.Null(temas.Leer(tema));
    }

    [Fact]
    public void Solo_las_cuentas_de_nodos_de_este_entorno_se_reconocen_al_sincronizar()
    {
        Assert.Equal("A4:CF:12:9B:3E:03", temas.MacDeUsuario("flowtwin-a4cf129b3e03"));
        Assert.Null(temas.MacDeUsuario("flowtwin-ingesta"));                    // la del backend, no de un nodo
        Assert.Null(temas.MacDeUsuario("flowtwin-sim-a4cf129b3e03"));          // de otro entorno
        Assert.Null(temas.MacDeUsuario("flowtwin-admin"));
    }

    [Theory]
    [InlineData("FlowTwin")]
    [InlineData("flow/twin")]
    [InlineData("flow+")]
    [InlineData("")]
    public void Un_prefijo_que_romperia_los_temas_no_se_acepta(string prefijo)
    {
        Assert.Throws<ArgumentException>(() => new TemasMqtt(prefijo));
    }

    private sealed class RelojFijo(DateTime t) : IReloj { public DateTime AhoraUtc { get; set; } = t; }

    [Fact]
    public void El_limite_cuenta_por_nodo_y_se_reinicia_cada_minuto()
    {
        var reloj = new RelojFijo(new DateTime(2026, 11, 15, 15, 0, 10, DateTimeKind.Utc));
        var limite = new LimitePorNodo(reloj, porMinuto: 2);

        Assert.True(limite.Permitir("A"));
        Assert.True(limite.Permitir("A"));
        Assert.False(limite.Permitir("A"));     // el tercero del minuto
        Assert.True(limite.Permitir("B"));      // otro nodo tiene su propia cuenta
        reloj.AhoraUtc = reloj.AhoraUtc.AddMinutes(1);
        Assert.True(limite.Permitir("A"));
    }

    [Fact]
    public void Cada_error_del_dominio_le_dice_al_nodo_que_hacer()
    {
        var invalido = RespuestaNodo.DesdeError("lecturas", new ValidacionException(new Dictionary<string, string[]> { ["rssi"] = ["mal"] }), 7);
        var rechazado = RespuestaNodo.DesdeError("latidos", new NoAutenticadoException());
        var conflicto = RespuestaNodo.DesdeError("lecturas", new ConflictoException("x"));

        Assert.Equal((RespuestaNodo.Invalido, 7L), (invalido.Estado, invalido.Secuencia!.Value));   // no reintentar: fallaría igual
        Assert.Equal(["mal"], invalido.Errores!["rssi"]);
        Assert.Equal(RespuestaNodo.Rechazado, rechazado.Estado);   // dejar de enviar y avisar
        Assert.Equal(RespuestaNodo.Error, conflicto.Estado);       // reintentar
    }
}
