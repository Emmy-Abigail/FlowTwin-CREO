using FlowTwin.Atenciones.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

public class AtencionTests
{
    private static readonly DateTime Inicio = new(2026, 10, 27, 15, 31, 0, DateTimeKind.Utc);

    private static Atencion Nueva() => Atencion.Abrir("A-0143", new DateOnly(2026, 10, 27), 1, 7, Inicio);

    [Fact]
    public void Codigo_es_prefijo_guion_y_cuatro_digitos() =>
        Assert.Equal("A-0143", Atencion.FormatearCodigo('A', 143));

    [Fact]
    public void Deshacer_dentro_de_30_segundos_la_deja_anulada()
    {
        var a = Nueva();

        a.Anular(Inicio.AddSeconds(29));

        Assert.False(a.EstaAbierta);
        Assert.Equal(MotivoCierreAtencion.Anulada, a.MotivoCierreId);
    }

    [Fact]
    public void Deshacer_despues_de_30_segundos_se_rechaza_RN04()
    {
        var a = Nueva();

        var error = Assert.Throws<ReglaNegocioException>(() => a.Anular(Inicio.AddSeconds(31)));

        Assert.Equal("RN04_DESHACER_VENCIDO", error.Codigo);
        Assert.True(a.EstaAbierta);
    }

    [Fact]
    public void Una_atencion_cerrada_no_se_vuelve_a_cerrar_RN05()
    {
        var a = Nueva();
        a.Cerrar(MotivoCierreAtencion.Devuelta, Inicio.AddMinutes(47), recibidaPor: 3);

        var error = Assert.Throws<ReglaNegocioException>(() => a.Cerrar(MotivoCierreAtencion.SinLecturas, Inicio.AddHours(2)));

        Assert.Equal("RN05_ATENCION_CERRADA", error.Codigo);
        Assert.Equal(MotivoCierreAtencion.Devuelta, a.MotivoCierreId);
        Assert.Equal(47, a.TotalMinutos(Inicio.AddHours(5)));
    }
}
