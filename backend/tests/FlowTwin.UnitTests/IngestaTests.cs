using FlowTwin.Atenciones.Application;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using Microsoft.Extensions.Options;

namespace FlowTwin.UnitTests;

/// <summary>Ingesta de lecturas: hora calculada en el servidor (ADR-14), idempotencia y formato del lote.</summary>
public class IngestaTests
{
    private static readonly DateTime Recibida = new(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LeidaEn_es_la_hora_de_llegada_menos_la_edad()
    {
        var lectura = LecturaBle.Desde(1, "UUID-0007", -61, edadMs: 350, Recibida);

        Assert.Equal(Recibida.AddMilliseconds(-350), lectura.LeidaEn);
        Assert.Equal("uuid-0007", lectura.UuidBle);   // normalizado: el mismo emisor no aparece dos veces
    }

    [Fact]
    public void El_identificador_del_emisor_sigue_el_contrato_con_el_hardware()
    {
        // Si esta prueba falla, se rompió el contrato con el firmware (docs/contrato-integracion-nodos.md)
        Assert.Equal("b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-7", EmisorBle.Identificador(major: 1, minor: 7));
    }

    [Fact]
    public void Un_lote_reenviado_se_reconoce_como_duplicado()
    {
        var registro = new RegistroSecuencias();

        Assert.True(registro.Registrar(nodoId: 1, secuencia: 18342));
        Assert.False(registro.Registrar(nodoId: 1, secuencia: 18342));
        Assert.True(registro.Registrar(nodoId: 2, secuencia: 18342));   // otro nodo, otra cuenta
    }

    [Fact]
    public void Tras_reiniciarse_el_nodo_su_secuencia_vuelve_a_0_y_se_acepta()
    {
        var registro = new RegistroSecuencias();
        for (var s = 500; s < 510; s++) registro.Registrar(1, s);

        Assert.True(registro.Registrar(1, 0));   // con la regla "rechazar lo menor" se perdería todo
        Assert.True(registro.Registrar(1, 1));
    }

    [Fact]
    public void Solo_recuerda_las_ultimas_secuencias_para_no_crecer_sin_limite()
    {
        var registro = new RegistroSecuencias();
        for (var s = 0; s <= RegistroSecuencias.Capacidad; s++) registro.Registrar(1, s);

        Assert.True(registro.Registrar(1, 0));   // la 0 ya salió de la ventana
    }

    [Fact]
    public void Si_guardar_fallo_la_secuencia_se_olvida_y_el_reintento_entra()
    {
        var registro = new RegistroSecuencias();
        registro.Registrar(1, 77);

        registro.Olvidar(1, 77);

        Assert.True(registro.Registrar(1, 77));
    }

    [Theory]
    [InlineData(5, 100)]          // RSSI positivo
    [InlineData(-61, -1)]         // edad negativa
    [InlineData(-61, 700_000)]    // más de 10 minutos
    public void Una_lectura_fuera_de_rango_no_es_valida(int rssi, int edadMs)
    {
        var validador = new LoteLecturasValidador(Options.Create(new OpcionesIngesta()));

        var r = validador.Validate(new LoteLecturasRequest("02:00:00:00:00:01", 1, [new("uuid", rssi, edadMs)]));

        Assert.False(r.IsValid);
    }

    [Fact]
    public void Un_lote_demasiado_grande_no_es_valido()
    {
        var validador = new LoteLecturasValidador(Options.Create(new OpcionesIngesta { MaxLecturasPorLote = 2 }));
        var lecturas = Enumerable.Range(0, 3).Select(i => new LecturaRequest($"uuid-{i}", -60, 100)).ToList();

        Assert.False(validador.Validate(new LoteLecturasRequest("02:00:00:00:00:01", 1, lecturas)).IsValid);
    }

    [Fact]
    public void Un_lote_vacio_es_valido_el_nodo_puede_no_ver_a_nadie()
    {
        var validador = new LoteLecturasValidador(Options.Create(new OpcionesIngesta()));

        Assert.True(validador.Validate(new LoteLecturasRequest("02:00:00:00:00:01", 1, [])).IsValid);
    }
}
