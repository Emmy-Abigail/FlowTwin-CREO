using FlowTwin.Atenciones.Contracts;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.Monitoreo.Domain;
using FlowTwin.Reportes.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>F5 y RF09 (ADR-25): alertas del Operador, zona fuera de servicio y cierres sin devolución.</summary>
public class AlertasTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 20, 0, 0, DateTimeKind.Utc);
    private static readonly OpcionesAlertas Opciones = new();
    private const int Sala = 3, Modulo2 = 7;

    private static readonly ZonaSemaforo[] Zonas =
    [
        new(Sala, "Sala de espera", "ESPERA", null),
        new(Modulo2, "Módulo 2", "SERVICIO", null),
    ];

    /// <summary>Una atención en la Sala desde hace «min» minutos, vista por un nodo hace «vistoHace».</summary>
    private static AtencionEnCurso EnSala(long id, int min, int vistoHace = 0) =>
        new(id, $"A-{id:D4}", 1, Ahora.AddMinutes(-min), [new EstadiaInfo(Sala, Ahora.AddMinutes(-min), null)], Ahora.AddMinutes(-vistoHace), 1);

    private static IReadOnlyList<AlertaPiso> Alertas(IReadOnlyList<AtencionEnCurso> atenciones, params NodoZona[] nodos)
    {
        var estado = CalculoEstado.Calcular(1, Zonas, nodos, atenciones, Ahora);
        return CalculoAlertas.Calcular(estado, atenciones, Opciones, Ahora);
    }

    [Fact]
    public void La_espera_alerta_a_los_40_y_es_critica_a_los_60_pero_no_avisa_por_esperas_normales()
    {
        var a = Alertas([EnSala(1, 25), EnSala(2, 39), EnSala(3, 45), EnSala(4, 61)]);

        Assert.Equal(["CRITICA", "ALTA"], a.Where(x => x.Tipo == "ESPERA_LARGA").Select(x => x.Nivel));
        Assert.DoesNotContain(a, x => x.AtencionId is 1 or 2);   // 25 y 39 min: espera normal, sin alerta
        Assert.Equal("A-0004", a[0].Codigo);                // lo más urgente primero
    }

    [Fact]
    public void Al_subir_de_nivel_la_clave_cambia_para_que_vuelva_a_sonar()
    {
        var a40 = Alertas([EnSala(1, 41)]).Single();
        var a60 = Alertas([EnSala(1, 61)]).Single();

        Assert.NotEqual(a40.Clave, a60.Clave);
    }

    [Fact]
    public void Quien_ya_esta_en_consulta_no_genera_alerta_de_espera()
    {
        var enConsulta = new AtencionEnCurso(1, "A-0001", 1, Ahora.AddMinutes(-90),
            [new EstadiaInfo(Sala, Ahora.AddMinutes(-90), Ahora.AddMinutes(-30)), new EstadiaInfo(Modulo2, Ahora.AddMinutes(-30), null)], Ahora, 1);

        Assert.Empty(Alertas([enConsulta]));
    }

    [Fact]
    public void Sin_lecturas_30_min_avisa_que_pudo_irse_sin_devolver_la_tarjeta()
    {
        var a = Alertas([EnSala(1, 5, vistoHace: 0), EnSala(2, 50, vistoHace: 31)]);

        var sin = Assert.Single(a, x => x.Tipo == "TARJETA_SIN_DEVOLVER");
        Assert.Equal(2, sin.AtencionId);
        Assert.Equal(Ahora.AddMinutes(-31), sin.Desde);   // desde la última vez que se la vio
    }

    [Fact]
    public void Un_paciente_sin_lecturas_no_se_repite_como_espera_larga()
    {
        var a = Alertas([EnSala(1, 70, vistoHace: 45)]);

        Assert.Equal("TARJETA_SIN_DEVOLVER", Assert.Single(a).Tipo);   // una sola alerta: la que pide actuar
    }

    [Fact]
    public void Un_nodo_sin_senal_es_una_alerta_y_uno_en_linea_no()
    {
        var a = Alertas([],
            new NodoZona(1, "N-01", Sala, 0, "EN_LINEA", Ahora),
            new NodoZona(2, "N-02", Modulo2, 0, "SIN_SENAL", Ahora.AddMinutes(-5)));

        Assert.Equal("N-02", Assert.Single(a).Nodo);
    }

    [Fact]
    public void Una_zona_fuera_de_servicio_se_marca_en_el_estado_con_su_motivo()
    {
        var estado = CalculoEstado.Calcular(1, Zonas, [], [], Ahora,
            [new PausaInfo(Modulo2, "LIMPIEZA", null, Ahora.AddMinutes(-10), null, 2)]);

        var z = estado.Zonas.Single(x => x.ZonaId == Modulo2);
        Assert.True(z.Pausada);
        Assert.Equal("LIMPIEZA", z.MotivoPausa);
        Assert.False(estado.Zonas.Single(x => x.ZonaId == Sala).Pausada);
    }

    // ---------- Pausa de zona (dominio)
    [Fact]
    public void Con_el_motivo_Otro_hay_que_escribir_cual()
    {
        Assert.Equal("PAUSA_SIN_DETALLE", Assert.Throws<ReglaNegocioException>(() => PausaZona.Iniciar(1, MotivoPausa.Otro, "  ", 2, Ahora)).Codigo);
        Assert.Equal("Fuga de agua", PausaZona.Iniciar(1, MotivoPausa.Otro, " Fuga de agua ", 2, Ahora).Detalle);
    }

    [Fact]
    public void Una_zona_que_ya_esta_en_servicio_no_se_reanuda_dos_veces()
    {
        var p = PausaZona.Iniciar(1, MotivoPausa.Limpieza, null, 2, Ahora);
        p.Finalizar(Ahora.AddMinutes(20));

        Assert.Throws<ConflictoException>(() => p.Finalizar(Ahora.AddMinutes(21)));
    }

    [Fact]
    public void El_codigo_del_catalogo_se_convierte_al_valor_sin_importar_mayusculas()
    {
        Assert.Equal(MotivoPausa.Limpieza, CatalogoExtensiones.DesdeCodigo<MotivoPausa>("limpieza"));
        Assert.Null(CatalogoExtensiones.DesdeCodigo<MotivoPausa>("VACACIONES"));
    }

    // ---------- Cierre sin devolución
    [Fact]
    public void Cerrada_sin_devolver_termina_cuando_se_vio_al_paciente_por_ultima_vez()
    {
        var a = Atencion.Abrir("A-0031", new DateOnly(2026, 11, 15), 1, 7, Ahora.AddMinutes(-90));
        a.UltimaLecturaEn = Ahora.AddMinutes(-40);

        var fin = a.CerrarSinDevolucion(MotivoCierreAtencion.SinLecturas, ultimaEstadiaSalida: Ahora.AddMinutes(-41));

        Assert.Equal(Ahora.AddMinutes(-40), fin);   // no «ahora»: el tiempo total no se infla
        Assert.Equal(MotivoCierreAtencion.SinLecturas, a.MotivoCierreId);
    }

    [Fact]
    public void Si_nunca_se_le_detecto_termina_en_la_entrega()
    {
        var a = Atencion.Abrir("A-0032", new DateOnly(2026, 11, 15), 1, 7, Ahora.AddMinutes(-90));

        Assert.Equal(a.InicioEn, a.CerrarSinDevolucion(MotivoCierreAtencion.CierreJornada, null));
    }

    // ---------- Historial: horas fuera de servicio
    [Fact]
    public void El_mapa_marca_la_hora_que_estuvo_fuera_de_servicio_sin_cambiar_su_valor()
    {
        var lima = new ZonaHoraria("America/Lima");
        var dia = new DateOnly(2026, 11, 9);
        var cobertura = new[] { new Tramo(lima.AUtc(dia, 0), lima.AUtc(dia, 23)) };
        var estadias = new[] { new Tramo(lima.AUtc(dia, 10), lima.AUtc(dia, 11)) };
        var fuera = new[] { new Tramo(lima.AUtc(dia, 10).AddMinutes(10), lima.AUtc(dia, 10).AddMinutes(50)) };   // 40 de 60 min

        var (_, filas) = CalculoHistorial.Mapa(new Periodo(dia, dia), 10, 11, estadias, cobertura, lima.AUtc(dia, 23), lima, fuera);

        Assert.True(filas[0].Celdas[0].FueraDeServicio);
        Assert.Equal(1.0, filas[0].Celdas[0].Personas);   // la persona detectada sigue contando
        Assert.False(filas[0].Celdas[1].FueraDeServicio);
    }
}
