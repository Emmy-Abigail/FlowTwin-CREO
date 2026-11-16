using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>Reglas de una zona del gemelo (gem.Zona, RN-13 y ADR-27).</summary>
public class ZonaTests
{
    private static Zona SalaDeEspera() => new()
    {
        PisoId = 1, TipoZonaId = TipoZona.Espera, Nombre = "Sala de espera",
        X = 19.3m, Y = 15.0m, AnchoM = 5.7m, FondoM = 5.3m, Aforo = 24,
    };

    [Fact]
    public void Una_zona_completa_es_valida()
    {
        Assert.Empty(SalaDeEspera().Problemas());
    }

    [Theory]
    [InlineData(TipoZona.Ingreso, true)]
    [InlineData(TipoZona.Espera, true)]
    [InlineData(TipoZona.Triaje, false)]
    [InlineData(TipoZona.Servicio, false)]
    [InlineData(TipoZona.Transito, false)]
    [InlineData(TipoZona.Salida, false)]
    public void Solo_donde_la_gente_se_acumula_hay_aforo_ADR27(TipoZona tipo, bool tieneAforo)
    {
        Assert.Equal(tieneAforo, tipo.TieneAforo());
    }

    [Fact]
    public void Una_zona_de_espera_con_el_aforo_por_definir_es_valida()
    {
        var zona = SalaDeEspera();
        zona.Aforo = null;

        Assert.Empty(zona.Problemas());
    }

    [Fact]
    public void Un_aforo_de_cero_no_es_valido()
    {
        var zona = SalaDeEspera();
        zona.Aforo = 0;

        Assert.Contains(zona.Problemas(), p => p.Contains("1 o más"));
    }

    [Theory]
    [InlineData(TipoZona.Servicio)]
    [InlineData(TipoZona.Transito)]
    public void Un_consultorio_o_un_hall_con_aforo_no_es_valido(TipoZona tipo)
    {
        var zona = new Zona { TipoZonaId = tipo, Nombre = "X", AnchoM = 1, FondoM = 1, Aforo = 4 };

        Assert.Contains(zona.Problemas(), p => p.Contains("no tiene aforo"));
    }

    [Fact]
    public void Una_zona_nueva_desde_Configuracion_es_valida_y_no_guarda_aforo_donde_no_va()
    {
        Assert.Empty(Zona.Nueva(1, " Sala VIP ", TipoZona.Espera, 8).Problemas());
        Assert.Equal("Sala VIP", Zona.Nueva(1, " Sala VIP ", TipoZona.Espera, 8).Nombre);
        Assert.Null(Zona.Nueva(1, "Consultorio 3", TipoZona.Servicio, 4).Aforo);
    }

    [Fact]
    public void Devuelve_todos_los_problemas_juntos()
    {
        var zona = new Zona { TipoZonaId = TipoZona.Servicio, Nombre = "", X = -1, AnchoM = 0, Aforo = 3 };

        Assert.True(zona.Problemas().Count >= 4);   // nombre, posición, tamaño y aforo donde no va
    }

    [Fact]
    public void Validar_lanza_RN13_ZONA_INVALIDA_con_la_lista_de_problemas()
    {
        var zona = SalaDeEspera();
        zona.AnchoM = 0;

        Assert.Equal("RN13_ZONA_INVALIDA", Assert.Throws<ReglaNegocioException>(zona.Validar).Codigo);
    }

    [Theory]
    [InlineData(40, 60, 30, 22, 80, true)]
    [InlineData(60, 60, 30, 22, 80, false)]   // la crítica debe ser mayor que la alerta
    [InlineData(40, 60, 30, 24, 80, false)]   // hora fuera de rango
    [InlineData(40, 60, 30, 22, 5, false)]    // ámbar demasiado bajo
    public void Los_parametros_se_validan(short alta, short critica, short sinLecturas, byte hora, byte ambar, bool valido)
    {
        var p = new ParametrosSistema { EsperaAltaMin = alta, EsperaCriticaMin = critica, SinLecturasMin = sinLecturas, HoraCierre = hora, SemaforoAmbarPct = ambar };

        Assert.Equal(valido, p.Problemas().Count == 0);
    }
}
