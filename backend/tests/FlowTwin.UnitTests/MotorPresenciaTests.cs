using FlowTwin.Atenciones.Domain;

namespace FlowTwin.UnitTests;

/// <summary>
/// Motor de presencia (RN-10 a RN-12) con escenarios reales de la clínica. Como el motor es puro,
/// cada escenario de varios minutos corre en milisegundos.
/// </summary>
public class MotorPresenciaTests
{
    private const long A0143 = 143;
    private const int Admision = 1, Sala = 3, Triaje = 4;
    private static readonly DateTime T0 = new(2026, 11, 5, 15, 32, 10, DateTimeKind.Utc);

    private readonly MotorPresencia motor = new(ParametrosPresencia.PorDefecto);

    /// <summary>Una lectura cada 2 s de cada zona, como los nodos reales, durante 'segundos'.</summary>
    private List<DecisionPresencia> Senal(DateTime desde, int segundos, params (int zona, int rssi)[] zonas)
    {
        var decisiones = new List<DecisionPresencia>();
        for (var s = 0; s <= segundos; s += 2)
            foreach (var (zona, rssi) in zonas)
                decisiones.AddRange(motor.Registrar(A0143, zona, rssi, desde.AddSeconds(s)));
        return decisiones;
    }

    [Fact]
    public void La_primera_zona_se_confirma_tras_la_histeresis_y_se_fecha_al_inicio_RN10()
    {
        var decisiones = Senal(T0, 50, (Admision, -60));

        var entrada = Assert.IsType<EntradaZona>(Assert.Single(decisiones));
        Assert.Equal(Admision, entrada.ZonaId);
        Assert.Equal(T0, entrada.En);   // no T0 + 45 s: la espera no se infla con la confirmación
    }

    [Fact]
    public void El_ejemplo_del_documento_Admision_a_Sala_de_espera()
    {
        // N-01 (Admisión) la ve a -79 y N-02 (Sala) a -58 desde las 10:32:10
        motor.Restaurar(A0143, Admision, T0.AddMinutes(-2));

        var decisiones = Senal(T0, 50, (Admision, -79), (Sala, -58));

        Assert.Equal(2, decisiones.Count);
        Assert.Equal(new SalidaZona(A0143, T0), decisiones[0]);
        Assert.Equal(new EntradaZona(A0143, Sala, T0), decisiones[1]);
    }

    [Fact]
    public void Pasar_caminando_frente_a_otra_zona_no_registra_nada()
    {
        motor.Restaurar(A0143, Sala, T0.AddMinutes(-10));

        // 8 s frente al Triaje, que la ve más fuerte, y vuelve a la Sala
        var decisiones = Senal(T0, 8, (Sala, -75), (Triaje, -55));
        decisiones.AddRange(Senal(T0.AddSeconds(10), 120, (Sala, -58)));

        Assert.Empty(decisiones);
        Assert.Equal(Sala, motor.ZonaDe(A0143));
    }

    [Fact]
    public void Una_lectura_suelta_fuerte_no_le_gana_a_la_senal_sostenida()
    {
        motor.Restaurar(A0143, Sala, T0.AddMinutes(-10));

        var decisiones = new List<DecisionPresencia>();
        for (var s = 0; s <= 60; s += 2)
        {
            decisiones.AddRange(motor.Registrar(A0143, Sala, -60, T0.AddSeconds(s)));
            // Triaje: un pico de -50 cada 10 s, el resto del tiempo -85
            decisiones.AddRange(motor.Registrar(A0143, Triaje, s % 10 == 0 ? -50 : -85, T0.AddSeconds(s)));
        }

        Assert.Empty(decisiones);
    }

    [Fact]
    public void Una_senal_debajo_del_minimo_no_cuenta()
    {
        Assert.Empty(Senal(T0, 120, (Sala, -85)));   // RssiMinimo = -80
    }

    [Fact]
    public void Sin_lecturas_por_120_s_cierra_la_estadia_con_la_ultima_vez_vista()
    {
        motor.Restaurar(A0143, Sala, T0.AddMinutes(-10));
        Senal(T0, 20, (Sala, -60));   // la última lectura es a T0 + 20 s

        Assert.Empty(motor.Revisar(T0.AddSeconds(100)));   // aún no pasan 120 s
        var salida = Assert.Single(motor.Revisar(T0.AddSeconds(141)));

        Assert.Equal(new SalidaZona(A0143, T0.AddSeconds(20)), salida);
        Assert.Null(motor.ZonaDe(A0143));   // «sin zona», por ejemplo en el baño
    }

    [Fact]
    public void Al_reaparecer_tras_el_bano_vuelve_a_entrar_con_histeresis()
    {
        motor.Restaurar(A0143, Sala, T0.AddMinutes(-10));
        Senal(T0, 10, (Sala, -60));
        motor.Revisar(T0.AddMinutes(5));

        var entrada = Assert.IsType<EntradaZona>(Assert.Single(Senal(T0.AddMinutes(8), 50, (Sala, -60))));

        Assert.Equal(T0.AddMinutes(8), entrada.En);
    }

    [Fact]
    public void Una_lectura_atrasada_que_salio_de_la_ventana_no_altera_la_decision()
    {
        motor.Restaurar(A0143, Sala, T0.AddMinutes(-10));
        Senal(T0, 60, (Sala, -60));

        // Un nodo de Triaje reenvía su búfer: lecturas de hace 5 minutos, muy fuertes
        var decisiones = Senal(T0.AddMinutes(-5), 60, (Triaje, -40));

        Assert.Empty(decisiones);
        Assert.Equal(Sala, motor.ZonaDe(A0143));
    }

    [Fact]
    public void Reproducir_las_mismas_lecturas_da_las_mismas_decisiones()
    {
        // Lo que permite reconstruir el estado al reiniciar el servidor (ADR-08)
        var otro = new MotorPresencia(ParametrosPresencia.PorDefecto);
        var primeras = Senal(T0, 50, (Admision, -60));
        var repetidas = new List<DecisionPresencia>();
        for (var s = 0; s <= 50; s += 2) repetidas.AddRange(otro.Registrar(A0143, Admision, -60, T0.AddSeconds(s)));

        Assert.Equal(primeras, repetidas);
    }

    [Fact]
    public void Olvida_las_atenciones_cerradas_para_no_acumular_memoria()
    {
        motor.Restaurar(A0143, Sala, T0);
        motor.Restaurar(200, Sala, T0);

        motor.Olvidar(new HashSet<long> { 200 });

        Assert.Equal([200L], motor.AtencionesRastreadas);
    }

    [Fact]
    public void Un_evento_no_se_cierra_antes_de_su_entrada()
    {
        var evento = new EventoZona { EntradaEn = T0 };

        evento.Cerrar(T0.AddSeconds(-30));

        Assert.Equal(T0, evento.SalidaEn);
    }
}
