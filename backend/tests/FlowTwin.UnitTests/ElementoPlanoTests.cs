using FlowTwin.Gemelo.Domain;

namespace FlowTwin.UnitTests;

/// <summary>Muros, puertas y ambientes del plano (gem.ElementoPlano, ADR-17).</summary>
public class ElementoPlanoTests
{
    /// <summary>Un muro en L con una columna (hueco) adentro.</summary>
    private static PoligonoConHuecos MuroEnL() => new(
        [[-0.08, -0.08], [4.0, -0.08], [4.0, 0.15], [0.15, 0.15], [0.15, 3.0], [-0.08, 3.0]],
        [[[1.0, 0.0], [1.1, 0.0], [1.1, 0.1]]]);

    [Fact]
    public void Un_muro_guarda_el_rectangulo_que_lo_contiene_y_su_poligono()
    {
        var muro = ElementoPlano.Muro(1, MuroEnL());

        Assert.Equal(TipoElementoPlano.Muro, muro.TipoElementoPlanoId);
        Assert.Equal((-0.08m, -0.08m, 4.0m, 3.0m), (muro.X1, muro.Y1, muro.X2, muro.Y2));
        Assert.Empty(muro.Problemas());
    }

    [Fact]
    public void Un_muro_perimetral_admite_coordenadas_negativas()
    {
        // Los ejes pasan por el centro del muro: su mitad exterior queda antes del origen
        Assert.True(ElementoPlano.Muro(1, MuroEnL()).X1 < 0);
    }

    [Fact]
    public void La_geometria_ida_y_vuelta_conserva_puntos_y_huecos()
    {
        var forma = ElementoPlano.Muro(1, MuroEnL()).Forma()!;

        Assert.Equal(6, forma.Poligono.Count);
        Assert.Equal([4.0, 0.15], forma.Poligono[2]);
        Assert.Single(forma.Huecos);
    }

    [Fact]
    public void Un_poligono_con_menos_de_3_puntos_no_es_valido()
    {
        var muro = ElementoPlano.Muro(1, new PoligonoConHuecos([[0, 0], [1, 1]], []));

        Assert.Contains(muro.Problemas(), p => p.Contains("al menos 3 puntos"));
    }

    [Fact]
    public void Un_ambiente_necesita_nombre_y_esquinas_distintas()
    {
        var ambiente = new ElementoPlano { TipoElementoPlanoId = TipoElementoPlano.Ambiente, X1 = 2, Y1 = 2, X2 = 2, Y2 = 5 };

        var problemas = ambiente.Problemas();

        Assert.Contains(problemas, p => p.Contains("necesita un nombre"));
        Assert.Contains(problemas, p => p.Contains("esquinas opuestas distintas"));
    }

    [Fact]
    public void Una_puerta_es_un_segmento_de_dos_puntos_distintos()
    {
        var puerta = new ElementoPlano { TipoElementoPlanoId = TipoElementoPlano.Puerta, X1 = 3, Y1 = 4, X2 = 3, Y2 = 4 };

        Assert.Contains(puerta.Problemas(), p => p.Contains("dos puntos distintos"));
    }

    [Fact]
    public void Una_geometria_que_no_es_JSON_se_reporta_sin_romper()
    {
        var muro = new ElementoPlano { TipoElementoPlanoId = TipoElementoPlano.Muro, X2 = 1, Y2 = 1, Geometria = "{no es json" };

        Assert.Contains(muro.Problemas(), p => p.Contains("no es un JSON válido"));
    }
}
