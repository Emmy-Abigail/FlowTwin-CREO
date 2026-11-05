using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>Reglas de una zona del gemelo (gem.Zona y RN-13).</summary>
public class ZonaTests
{
    private static Zona SalaDeEspera() => new()
    {
        PisoId = 1, TipoZonaId = TipoZona.Espera, Nombre = "Sala de espera",
        X = 19.3m, Y = 15.0m, AnchoM = 5.7m, FondoM = 5.3m,
        Aforo = 24, UmbralMediaPct = 60, UmbralAltaPct = 85,
    };

    [Fact]
    public void Una_zona_completa_es_valida()
    {
        Assert.Empty(SalaDeEspera().Problemas());
    }

    [Theory]
    [InlineData(TipoZona.Ingreso, true)]
    [InlineData(TipoZona.Espera, true)]
    [InlineData(TipoZona.Servicio, true)]
    [InlineData(TipoZona.Transito, false)]
    [InlineData(TipoZona.Salida, false)]
    public void Solo_ingreso_espera_y_servicio_tienen_aforo_RN13(TipoZona tipo, bool tieneAforo)
    {
        Assert.Equal(tieneAforo, tipo.TieneAforo());
    }

    [Fact]
    public void Una_zona_de_espera_sin_aforo_no_es_valida_RN13()
    {
        var zona = SalaDeEspera();
        zona.Aforo = null;

        Assert.Contains(zona.Problemas(), p => p.Contains("aforo de 1 o más"));
    }

    [Fact]
    public void Un_hall_de_transito_con_aforo_no_es_valido_RN13()
    {
        var hall = new Zona
        {
            TipoZonaId = TipoZona.Transito, Nombre = "Hall principal", AnchoM = 6.2m, FondoM = 4.8m, Aforo = 30,
        };

        Assert.Contains(hall.Problemas(), p => p.Contains("no tiene aforo ni umbrales"));
    }

    [Theory]
    [InlineData(0, 85)]    // media debe ser mayor que 0
    [InlineData(90, 85)]   // media no puede superar a alta
    [InlineData(60, 120)]  // alta no puede pasar de 100
    public void Los_umbrales_deben_estar_ordenados(int media, int alta)
    {
        var zona = SalaDeEspera();
        zona.UmbralMediaPct = (byte)media;
        zona.UmbralAltaPct = (byte)alta;

        Assert.Contains(zona.Problemas(), p => p.Contains("0 < media ≤ alta ≤ 100"));
    }

    [Fact]
    public void Devuelve_todos_los_problemas_juntos_para_el_editor()
    {
        var zona = new Zona { TipoZonaId = TipoZona.Servicio, Nombre = "", X = -1, AnchoM = 0 };

        Assert.True(zona.Problemas().Count >= 4);   // nombre, posición, tamaño, aforo y umbrales
    }

    [Fact]
    public void Validar_lanza_RN13_ZONA_INVALIDA_con_la_lista_de_problemas()
    {
        var zona = SalaDeEspera();
        zona.AnchoM = 0;

        var ex = Assert.Throws<ReglaNegocioException>(zona.Validar);

        Assert.Equal("RN13_ZONA_INVALIDA", ex.Codigo);
    }
}
