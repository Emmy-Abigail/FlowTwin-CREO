using System.Text;
using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Reportes.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>Historial (P5): periodo, cifras del periodo, mapa día × hora con «sin datos» y CSV.</summary>
public class HistorialTests
{
    private static readonly ZonaHoraria Lima = new("America/Lima");
    private static readonly DateOnly Lunes = new(2026, 11, 9);

    /// <summary>Lima es UTC−5 todo el año: las 10:00 de Lima son las 15:00 UTC.</summary>
    private static DateTime L(DateOnly fecha, int hora, int minuto = 0) => Lima.AUtc(fecha, hora).AddMinutes(minuto);

    // ---------- Periodo
    [Fact]
    public void El_periodo_admite_hasta_31_dias_y_no_el_futuro()
    {
        var hoy = new DateOnly(2026, 11, 15);
        Assert.Equal(31, Periodo.Validar(hoy.AddDays(-30), hoy, hoy).Dias);
        Assert.Equal("PERIODO_INVALIDO", Assert.Throws<ReglaNegocioException>(() => Periodo.Validar(hoy.AddDays(-31), hoy, hoy)).Codigo);
        Assert.Throws<ReglaNegocioException>(() => Periodo.Validar(hoy, hoy.AddDays(-1), hoy));   // al revés
        Assert.Throws<ReglaNegocioException>(() => Periodo.Validar(hoy, hoy.AddDays(1), hoy));    // futuro
    }

    // ---------- Cifras
    [Fact]
    public void Mediana_y_percentil_90_son_valores_que_ocurrieron()
    {
        Assert.Null(CalculoHistorial.Mediana([]));
        Assert.Equal(15, CalculoHistorial.Mediana([10, 15, 120]));          // el caso extremo no la mueve
        Assert.Equal(12, CalculoHistorial.Mediana([10, 15, 14, 11]));       // (11 + 14) / 2 hacia abajo
        Assert.Equal(9, CalculoHistorial.Percentil90([.. Enumerable.Range(1, 10)]));
        Assert.Equal(1, CalculoHistorial.Percentil90([1]));
    }

    private static FilaHistorial Fila(int espera, int? consulta, int total, bool terminada = true) =>
        new(1, "A-0001", Lunes, L(Lunes, 9), terminada ? L(Lunes, 9, total) : null, terminada ? "FINALIZADA" : "EN_ESPERA",
            terminada ? "DEVUELTA" : null, espera, consulta, total);

    [Fact]
    public void El_resumen_cuenta_todas_pero_mide_solo_las_terminadas()
    {
        var r = CalculoHistorial.Resumen(
        [
            Fila(10, 12, 30), Fila(20, 10, 40), Fila(30, 15, 50),
            Fila(45, null, 45),                  // se fue sin entrar a consulta: no tiene «espera hasta ser atendido»
            Fila(90, null, 90, terminada: false),  // sigue abierta: aún no tiene tiempos finales
        ]);

        Assert.Equal(5, r.Atenciones);
        Assert.Equal(4, r.Terminadas);
        Assert.Equal(3, r.ConEspera);
        Assert.Equal(20, r.EsperaMediana);
        Assert.Equal(30, r.EsperaP90);
        Assert.Equal(42, r.TotalMediana);   // (40 + 45) / 2 hacia abajo
        Assert.Equal(50, r.TotalP90);
    }

    [Fact]
    public void La_fila_usa_la_misma_regla_que_el_detalle()
    {
        var inicio = L(Lunes, 9);
        var a = new AtencionDetalleInfo(1, "A-0001", Lunes, 1, 1, inicio, null, null,
            [new EstadiaInfo(3, inicio, inicio.AddMinutes(18)), new EstadiaInfo(7, inicio.AddMinutes(18), null)]);

        var f = CalculoHistorial.Fila(a, id => id == 7, inicio.AddMinutes(25));

        Assert.Equal((EstadoAtencion.EnAtencion, 18, (int?)7, 25), (f.Estado, f.EsperaMin, f.ConsultaMin, f.TotalMin));
        Assert.False(f.Terminada);
    }

    // ---------- Cobertura de los nodos
    [Fact]
    public void La_cobertura_une_los_nodos_de_la_zona_y_respeta_las_caidas()
    {
        var fin = L(Lunes, 12);
        var cobertura = CalculoHistorial.Cobertura(
        [
            new CambioNodo(1, 3, true, L(Lunes, 8)), new CambioNodo(1, 3, false, L(Lunes, 10)),     // N-01: 8 a 10
            new CambioNodo(2, 3, true, L(Lunes, 9, 30)), new CambioNodo(2, 3, false, L(Lunes, 10, 30)), // N-02: 9:30 a 10:30
            new CambioNodo(1, 3, true, L(Lunes, 11)),                                                // N-01 vuelve: 11 a 12
        ], fin);

        Assert.Equal([new Tramo(L(Lunes, 8), L(Lunes, 10, 30)), new Tramo(L(Lunes, 11), fin)], cobertura);
    }

    // ---------- Mapa
    private static readonly Periodo UnDia = new(Lunes, Lunes);

    [Fact]
    public void Cada_celda_son_minutos_paciente_entre_60_no_cuantos_pasaron()
    {
        // Tres pacientes de 20 min entre las 10 y las 11 = 1 persona en promedio; uno toda la hora de las 11
        Tramo[] estadias =
        [
            new(L(Lunes, 10, 0), L(Lunes, 10, 20)), new(L(Lunes, 10, 20), L(Lunes, 10, 40)), new(L(Lunes, 10, 40), L(Lunes, 11, 0)),
            new(L(Lunes, 11, 0), L(Lunes, 12, 0)),
        ];
        var cobertura = new[] { new Tramo(L(Lunes, 0), L(Lunes, 23)) };

        var (grupo, filas) = CalculoHistorial.Mapa(UnDia, 10, 12, estadias, cobertura, L(Lunes, 23), Lima);

        Assert.Equal(CalculoHistorial.PorFecha, grupo);
        Assert.Equal(Lunes, filas.Single().Fecha);
        Assert.Equal([(10, 1.0), (11, 1.0), (12, 0.0)], filas[0].Celdas.Select(c => (c.Hora, c.Personas!.Value)));
    }

    [Fact]
    public void Sin_nodo_en_linea_la_mayor_parte_de_la_hora_es_sin_datos_no_cero()
    {
        // Nodo en línea de 10:00 a 10:20 y desde 11:40: la hora de las 10 no alcanza la mitad, la de las 11 tampoco
        var cobertura = new[] { new Tramo(L(Lunes, 10), L(Lunes, 10, 20)), new Tramo(L(Lunes, 11, 40), L(Lunes, 14)) };

        var (_, filas) = CalculoHistorial.Mapa(UnDia, 10, 12, [], cobertura, L(Lunes, 14), Lima);

        Assert.Equal([EstadoCelda.SinDatos, EstadoCelda.SinDatos, EstadoCelda.Ok], filas[0].Celdas.Select(c => c.Estado));
        Assert.Null(filas[0].Celdas[0].Personas);
        Assert.Equal(0.0, filas[0].Celdas[2].Personas);   // con nodo y sin nadie: un cero de verdad
    }

    [Fact]
    public void La_hora_que_aun_no_llega_es_futuro_y_la_hora_en_curso_se_divide_entre_lo_que_va()
    {
        var cobertura = new[] { new Tramo(L(Lunes, 0), L(Lunes, 23)) };
        var estadias = new[] { new Tramo(L(Lunes, 10, 0), L(Lunes, 10, 15)) };

        var (_, filas) = CalculoHistorial.Mapa(UnDia, 10, 11, estadias, cobertura, L(Lunes, 10, 30), Lima);

        Assert.Equal(0.5, filas[0].Celdas[0].Personas);   // 15 de 30 minutos transcurridos
        Assert.Equal(EstadoCelda.Futuro, filas[0].Celdas[1].Estado);
    }

    [Fact]
    public void Con_mas_de_7_dias_cada_fila_es_un_dia_de_la_semana_con_el_promedio_de_sus_fechas()
    {
        var periodo = new Periodo(Lunes, Lunes.AddDays(13));   // dos semanas
        var cobertura = new List<Tramo> { new(L(Lunes, 0), L(Lunes.AddDays(14), 0)) };
        // Los dos lunes: 1 persona toda la hora y luego 2 personas toda la hora → promedio 1,5
        Tramo[] estadias =
        [
            new(L(Lunes, 10), L(Lunes, 11)),
            new(L(Lunes.AddDays(7), 10), L(Lunes.AddDays(7), 11)), new(L(Lunes.AddDays(7), 10), L(Lunes.AddDays(7), 11)),
        ];

        var (grupo, filas) = CalculoHistorial.Mapa(periodo, 10, 10, estadias, cobertura, L(Lunes.AddDays(14), 0), Lima);

        Assert.Equal(CalculoHistorial.PorDiaSemana, grupo);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], filas.Select(f => f.DiaSemana!.Value));
        Assert.Equal(1.5, filas[0].Celdas[0].Personas);
    }

    [Fact]
    public void En_el_promedio_por_dia_de_semana_las_fechas_sin_datos_no_cuentan_como_cero()
    {
        var periodo = new Periodo(Lunes, Lunes.AddDays(13));
        // El primer lunes no hubo nodo; el segundo, 2 personas → el lunes promedia 2, no 1
        var cobertura = new List<Tramo> { new(L(Lunes.AddDays(1), 0), L(Lunes.AddDays(14), 0)) };
        Tramo[] estadias = [new(L(Lunes.AddDays(7), 10), L(Lunes.AddDays(7), 11)), new(L(Lunes.AddDays(7), 10), L(Lunes.AddDays(7), 11))];

        var (_, filas) = CalculoHistorial.Mapa(periodo, 10, 10, estadias, cobertura, L(Lunes.AddDays(14), 0), Lima);

        Assert.Equal(2.0, filas[0].Celdas[0].Personas);
    }

    [Fact]
    public void El_horario_va_de_la_primera_entrega_a_la_ultima_salida_en_hora_de_Lima()
    {
        AtencionDetalleInfo A(int hIni, int hFin) => new(1, "A", Lunes, 1, 1, L(Lunes, hIni, 10), L(Lunes, hFin, 5), "DEVUELTA", []);

        Assert.Equal((8, 17), CalculoHistorial.Horario([A(9, 10), A(8, 9), A(16, 17)], L(Lunes, 20), Lima));
        Assert.Null(CalculoHistorial.Horario([], L(Lunes, 20), Lima));
    }

    // ---------- CSV
    [Fact]
    public void El_csv_es_anonimo_en_hora_de_Lima_y_con_BOM_para_Excel()
    {
        var bytes = CsvHistorial.Generar(
        [
            new FilaHistorial(1, "A-0041", Lunes, L(Lunes, 15, 48), L(Lunes, 16, 21), "FINALIZADA", "DEVUELTA", 14, 12, 33),
            new FilaHistorial(2, "A-0042", Lunes, L(Lunes, 16, 2), null, "EN_ESPERA", null, 14, null, 19),
        ], Lima);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lineas = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Código,Fecha,Entrega,Fin,Estado,Cierre,Espera (min),Consulta (min),Total (min)", lineas[0]);
        Assert.Equal("A-0041,2026-11-09,15:48,16:21,Finalizada,Tarjeta devuelta,14,12,33", lineas[1]);
        Assert.Equal("A-0042,2026-11-09,16:02,,En espera,,14,,19", lineas[2]);
    }

    [Fact]
    public void Un_campo_que_Excel_tomaria_como_formula_se_neutraliza()
    {
        Assert.Equal("'=1+1", CsvHistorial.Campo("=1+1"));
        Assert.Equal("\"a,b\"", CsvHistorial.Campo("a,b"));
        Assert.Equal("\"di \"\"hola\"\"\"", CsvHistorial.Campo("di \"hola\""));
    }
}
