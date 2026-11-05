using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.Monitoreo.Domain;

namespace FlowTwin.UnitTests;

/// <summary>Estado del piso (F-11): espera y consulta (RN-06), semáforo y lo que se envía por el hub.</summary>
public class EstadoPisoTests
{
    private const int Admision = 1, Hall = 2, Sala = 3, Modulo2 = 7;
    private static readonly DateTime Ahora = new(2026, 11, 5, 16, 0, 0, DateTimeKind.Utc);

    private static readonly ZonaSemaforo[] Zonas =
    [
        new(Admision, "Admisión y caja", "INGRESO", 6, 60, 85),
        new(Hall, "Hall principal", "TRANSITO", null, null, null),
        new(Sala, "Sala de espera", "ESPERA", 10, 60, 85),
        new(Modulo2, "Módulo 2", "SERVICIO", 6, 60, 85),
    ];

    private static NodoZona Nodo(int zona, string estado = "EN_LINEA") => new(zona, $"N-0{zona}", zona, 0, estado, Ahora);

    private static readonly NodoZona[] TodosEnLinea = [Nodo(Admision), Nodo(Sala), Nodo(Modulo2)];

    private static AtencionEnCurso Atencion(string codigo, int minutosAtras, params EstadiaInfo[] estadias) =>
        new(codigo.GetHashCode(), codigo, 1, Ahora.AddMinutes(-minutosAtras), estadias);

    private static EstadiaInfo En(int zona, int desdeMin, int? hastaMin = null) =>
        new(zona, Ahora.AddMinutes(-desdeMin), hastaMin is null ? null : Ahora.AddMinutes(-hastaMin.Value));

    [Fact]
    public void La_espera_sigue_corriendo_mientras_no_entra_a_consulta_RN06()
    {
        var a = Atencion("A-0143", 30, En(Admision, 30, 27), En(Sala, 27));

        var estado = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [a], Ahora).Atenciones.Single();

        Assert.Equal(EstadoAtencion.EnEspera, estado.Estado);
        Assert.Equal(Sala, estado.ZonaActual);
        Assert.Equal(30, estado.EsperaMin);
    }

    [Fact]
    public void La_espera_se_detiene_al_entrar_a_una_zona_Servicio_y_corre_la_consulta_RN06()
    {
        // Entregada hace 40 min; entró a Módulo 2 hace 12: espera 28 y consulta 12
        var a = Atencion("A-0143", 40, En(Admision, 40, 37), En(Sala, 37, 12), En(Modulo2, 12));

        var estado = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [a], Ahora).Atenciones.Single();

        Assert.Equal(EstadoAtencion.EnAtencion, estado.Estado);
        Assert.Equal(28, estado.EsperaMin);
        Assert.Equal(12, estado.ConsultaMin);
    }

    [Fact]
    public void Despues_de_la_consulta_queda_como_atendido()
    {
        var a = Atencion("A-0143", 60, En(Sala, 60, 20), En(Modulo2, 20, 5), En(Admision, 5));

        Assert.Equal(EstadoAtencion.Atendido, CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [a], Ahora).Atenciones.Single().Estado);
    }

    [Theory]
    [InlineData(5, Nivel.Baja)]    // 50 %  < 60
    [InlineData(6, Nivel.Media)]   // 60 %  ≥ 60 y < 85
    [InlineData(9, Nivel.Alta)]    // 90 %  ≥ 85
    public void El_semaforo_sigue_los_umbrales_de_la_zona(int personas, string esperado)
    {
        var enSala = Enumerable.Range(0, personas).Select(i => Atencion($"A-{i:D4}", 10, En(Sala, 5))).ToList();

        var sala = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, enSala, Ahora).Zonas.Single(z => z.ZonaId == Sala);

        Assert.Equal(personas, sala.Ocupacion);
        Assert.Equal(esperado, sala.Nivel);
    }

    [Fact]
    public void Una_zona_sin_nodo_en_linea_no_muestra_un_cero_falso_sino_SIN_DATOS()
    {
        var estado = CalculoEstado.Calcular(1, Zonas, [Nodo(Admision), Nodo(Sala, "SIN_SENAL")], [], Ahora);

        Assert.Equal(Nivel.SinDatos, estado.Zonas.Single(z => z.ZonaId == Sala).Nivel);       // su nodo está sin señal
        Assert.Equal(Nivel.SinDatos, estado.Zonas.Single(z => z.ZonaId == Modulo2).Nivel);    // no tiene nodo
        Assert.Equal(Nivel.Baja, estado.Zonas.Single(z => z.ZonaId == Admision).Nivel);
    }

    [Fact]
    public void Transito_no_tiene_semaforo_RN13()
    {
        Assert.Equal(Nivel.Ninguno, CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [], Ahora).Zonas.Single(z => z.ZonaId == Hall).Nivel);
    }

    [Fact]
    public void Sin_pacientes_los_promedios_son_nulos_y_no_un_cero_que_engane()
    {
        var kpis = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [], Ahora).Kpis;

        Assert.Equal(new KpisPiso(0, 0, null, null), kpis);
    }

    [Fact]
    public void Una_estadia_en_otro_piso_no_cuenta_en_este()
    {
        var a = Atencion("A-0143", 20, En(zona: 99, desdeMin: 5));

        var estado = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [a], Ahora);

        Assert.Null(estado.Atenciones.Single().ZonaActual);
        Assert.All(estado.Zonas, z => Assert.Equal(0, z.Ocupacion));
    }

    // ---- Diferencias: lo que el notificador envía por SignalR

    [Fact]
    public void La_primera_foto_se_envia_completa_y_una_igual_no_envia_nada()
    {
        var foto = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [Atencion("A-0143", 10, En(Sala, 5))], Ahora);

        Assert.NotEmpty(Diferencias.Entre(null, foto));
        Assert.Empty(Diferencias.Entre(foto, foto));
    }

    [Fact]
    public void Si_un_paciente_cambia_de_zona_solo_se_envia_lo_que_cambio()
    {
        var antes = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [Atencion("A-0143", 30, En(Sala, 25))], Ahora);
        var despues = CalculoEstado.Calcular(1, Zonas, TodosEnLinea,
            [Atencion("A-0143", 30, En(Sala, 25, 1), En(Modulo2, 1))], Ahora);

        var mensajes = Diferencias.Entre(antes, despues);

        Assert.Equal(2, mensajes.Count(m => m.Metodo == "OcupacionZonaCambiada"));   // la Sala baja y Módulo 2 sube
        Assert.Single(mensajes, m => m.Metodo == "AtencionActualizada");
        Assert.DoesNotContain(mensajes, m => m.Metodo == "NodoEstadoCambiado");
    }

    [Fact]
    public void Que_pase_un_minuto_de_espera_no_genera_mensajes_por_paciente()
    {
        var a = Atencion("A-0143", 30, En(Sala, 25));
        var antes = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [a], Ahora);
        var despues = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [a], Ahora.AddMinutes(1));

        Assert.DoesNotContain(Diferencias.Entre(antes, despues), m => m.Metodo == "AtencionActualizada");
    }

    [Fact]
    public void Al_devolver_la_tarjeta_el_paciente_se_informa_como_finalizado()
    {
        var antes = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [Atencion("A-0143", 30, En(Sala, 25))], Ahora);
        var despues = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [], Ahora);

        var fin = Assert.Single(Diferencias.Entre(antes, despues), m => m.Metodo == "AtencionActualizada");
        Assert.Contains("FINALIZADA", fin.Contenido.ToString());
    }

    [Fact]
    public void Si_un_nodo_se_cae_se_avisa_y_su_zona_pasa_a_SIN_DATOS()
    {
        var antes = CalculoEstado.Calcular(1, Zonas, TodosEnLinea, [], Ahora);
        var despues = CalculoEstado.Calcular(1, Zonas, [Nodo(Admision), Nodo(Sala, "SIN_SENAL"), Nodo(Modulo2)], [], Ahora);

        var mensajes = Diferencias.Entre(antes, despues);

        Assert.Single(mensajes, m => m.Metodo == "NodoEstadoCambiado");
        Assert.Single(mensajes, m => m.Metodo == "OcupacionZonaCambiada" && m.Contenido.ToString()!.Contains(Nivel.SinDatos));
    }
}
