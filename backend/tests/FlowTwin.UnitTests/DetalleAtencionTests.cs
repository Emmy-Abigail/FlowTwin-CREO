using FlowTwin.Atenciones.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.Reportes.Domain;

namespace FlowTwin.UnitTests;

/// <summary>Detalle de una atención (P6): estado, tiempos con la misma regla del gemelo (RN-06) y recorrido.</summary>
public class DetalleAtencionTests
{
    private const int Admision = 1, Hall = 2, Sala = 3, Triaje = 4, Modulo2 = 7;
    private static readonly DateTime Inicio = new(2026, 11, 15, 20, 23, 0, DateTimeKind.Utc);

    private static readonly ZonaRecorrido[] Zonas =
    [
        new(Admision, "Admisión y caja", "INGRESO", 1),
        new(Hall, "Hall principal", "TRANSITO", 1),
        new(Sala, "Sala de espera", "ESPERA", 1),
        new(Triaje, "Triaje", "TRIAJE", 1),
        new(Modulo2, "Módulo 2 (consultorio)", "SERVICIO", 1),
    ];

    /// <summary>Una estadía entre dos minutos contados desde la entrega; sin «hasta», sigue ahí.</summary>
    private static EstadiaInfo En(int zona, int desde, int? hasta = null) =>
        new(zona, Inicio.AddMinutes(desde), hasta is null ? null : Inicio.AddMinutes(hasta.Value));

    private static AtencionDetalleInfo Atencion(int? finMin, string? motivo, params EstadiaInfo[] estadias) =>
        new(42, "A-0012", new DateOnly(2026, 11, 15), 1, 1, Inicio, finMin is null ? null : Inicio.AddMinutes(finMin.Value), motivo, estadias);

    private static AtencionDetalle Detalle(AtencionDetalleInfo a, int ahoraMin = 60) =>
        CalculoDetalle.Construir(a, Zonas, "Sede Principal", "T-01", Inicio.AddMinutes(ahoraMin));

    [Fact]
    public void Una_atencion_devuelta_tiene_su_espera_consulta_y_total()
    {
        // Admisión 0–2, Sala 2–18, Hall 18–19, Módulo 2 19–33, Hall 33–35, devuelve a los 35
        var d = Detalle(Atencion(35, "DEVUELTA", En(Admision, 0, 2), En(Sala, 2, 18), En(Hall, 18, 19), En(Modulo2, 19, 33), En(Hall, 33, 35)));

        Assert.Equal(EstadoAtencion.Finalizada, d.Estado);
        Assert.Equal("DEVUELTA", d.Motivo);
        Assert.Equal(19, d.EsperaMin);     // hasta la primera entrada a Servicio
        Assert.Equal(14, d.ConsultaMin);
        Assert.Equal(35, d.TotalMin);      // medido hasta la devolución, no hasta «ahora»
        Assert.Equal(["Admisión y caja", "Sala de espera", "Hall principal", "Módulo 2 (consultorio)", "Hall principal"], d.Estadias.Select(e => e.Zona));
        Assert.Equal("SERVICIO", d.Estadias[3].Tipo);
        Assert.Equal(("Sede Principal", "T-01"), (d.Sede, d.Tarjeta));
    }

    [Fact]
    public void El_triaje_no_detiene_la_espera_ni_suma_a_la_consulta_ADR26()
    {
        // Sala 4–16, Triaje 17–20, Sala otra vez 20–39, Módulo 2 desde 39: la espera es hasta el médico (39)
        var d = Detalle(Atencion(null, null, En(Sala, 4, 16), En(Triaje, 17, 20), En(Sala, 20, 39), En(Modulo2, 39)), ahoraMin: 50);

        Assert.Equal(39, d.EsperaMin);
        Assert.Equal(11, d.ConsultaMin);   // solo Módulo 2: los 3 min de triaje no son consulta
        Assert.Equal(EstadoAtencion.EnAtencion, d.Estado);
    }

    [Fact]
    public void En_espera_la_espera_corre_hasta_ahora_y_la_consulta_aun_no_existe()
    {
        var d = Detalle(Atencion(null, null, En(Admision, 0, 2), En(Sala, 3)), ahoraMin: 19);

        Assert.Equal(EstadoAtencion.EnEspera, d.Estado);
        Assert.Equal(19, d.EsperaMin);
        Assert.Null(d.ConsultaMin);        // «—», no un 0 que parezca una consulta de cero minutos
        Assert.Equal(19, d.TotalMin);
        Assert.Null(d.Motivo);
    }

    [Fact]
    public void En_un_consultorio_esta_en_atencion()
    {
        var d = Detalle(Atencion(null, null, En(Sala, 0, 10), En(Modulo2, 10)), ahoraMin: 15);

        Assert.Equal(EstadoAtencion.EnAtencion, d.Estado);
        Assert.Equal(10, d.EsperaMin);
        Assert.Equal(5, d.ConsultaMin);
    }

    [Fact]
    public void Si_vuelve_a_la_sala_despues_de_la_consulta_queda_atendido_y_la_espera_no_vuelve_a_correr()
    {
        var d = Detalle(Atencion(null, null, En(Sala, 0, 10), En(Modulo2, 10, 20), En(Sala, 20)), ahoraMin: 40);

        Assert.Equal(EstadoAtencion.Atendido, d.Estado);
        Assert.Equal(10, d.EsperaMin);     // RN-06: solo hasta la PRIMERA consulta
        Assert.Equal(10, d.ConsultaMin);
    }

    [Fact]
    public void Una_entrega_anulada_no_tiene_tiempos_RN04()
    {
        var d = Detalle(Atencion(0, "ANULADA"));

        Assert.Equal(EstadoAtencion.Anulada, d.Estado);
        Assert.Null(d.EsperaMin);
        Assert.Null(d.ConsultaMin);
        Assert.Null(d.TotalMin);
    }

    [Fact]
    public void Una_zona_que_ya_no_se_encuentra_se_muestra_sin_inventarle_un_tipo()
    {
        var d = Detalle(Atencion(10, "DEVUELTA", En(99, 0, 10)));

        var e = Assert.Single(d.Estadias);
        Assert.Equal(("Zona 99", CalculoDetalle.TipoDesconocido), (e.Zona, e.Tipo));
        Assert.Equal(10, d.EsperaMin);     // nunca entró a Servicio: esperó todo el tiempo
        Assert.Null(d.ConsultaMin);
    }
}
