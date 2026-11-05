using FlowTwin.Dispositivos.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.UnitTests;

/// <summary>Nodo ESP32: identificación, clave (sección Seguridad → Nodos) y ciclo de vida (F-15).</summary>
public class NodoTests
{
    private static Nodo NodoValido() => new() { Id = 3, Codigo = "N-03", Mac = "A4:CF:12:9B:3E:03", ZonaId = 7 };

    [Theory]
    [InlineData("a4cf129b3e03")]
    [InlineData("A4-CF-12-9B-3E-03")]
    [InlineData(" a4:cf:12:9b:3e:03 ")]
    public void La_MAC_se_normaliza_a_mayusculas_con_dos_puntos(string texto)
    {
        Assert.Equal("A4:CF:12:9B:3E:03", Nodo.NormalizarMac(texto));
    }

    [Theory]
    [InlineData("A4:CF:12:9B:3E")]       // le falta un byte
    [InlineData("G4:CF:12:9B:3E:03")]    // G no es hexadecimal
    public void Una_MAC_mal_formada_no_se_normaliza(string texto)
    {
        Assert.Null(Nodo.NormalizarMac(texto));
    }

    [Theory]
    [InlineData("n3", "N-03")]
    [InlineData("N 12", "N-12")]
    [InlineData("7", "N-07")]
    public void El_codigo_se_normaliza(string texto, string esperado)
    {
        Assert.Equal(esperado, Nodo.NormalizarCodigo(texto));
    }

    [Fact]
    public void La_clave_se_muestra_una_vez_y_solo_se_guarda_su_hash()
    {
        var nodo = NodoValido();

        var clave = nodo.AsignarClaveNueva();

        Assert.StartsWith("nk_", clave);
        Assert.Equal(32, nodo.ClaveApiHash.Length);                       // SHA-256
        Assert.NotEqual(clave, Convert.ToBase64String(nodo.ClaveApiHash));
        Assert.True(nodo.ClaveCoincide(clave));
        Assert.False(nodo.ClaveCoincide(clave + "x"));
    }

    [Fact]
    public void Dos_claves_generadas_nunca_son_iguales()
    {
        Assert.NotEqual(ClaveNodo.Generar(), ClaveNodo.Generar());
    }

    [Fact]
    public void Regenerar_la_clave_invalida_la_anterior()
    {
        var nodo = NodoValido();
        var vieja = nodo.AsignarClaveNueva();

        var nueva = nodo.AsignarClaveNueva();

        Assert.False(nodo.ClaveCoincide(vieja));
        Assert.True(nodo.ClaveCoincide(nueva));
    }

    [Fact]
    public void Dar_de_baja_invalida_la_clave_y_deja_el_evento()
    {
        var nodo = NodoValido();
        var clave = nodo.AsignarClaveNueva();

        var evento = nodo.DarDeBaja("Dañado");

        Assert.Equal(EstadoNodo.Baja, nodo.EstadoNodoId);
        Assert.Equal(EstadoNodo.Baja, evento.EstadoNodoId);
        Assert.Equal(3, evento.NodoId);
        Assert.False(nodo.ClaveCoincide(clave));
    }

    [Fact]
    public void Un_nodo_de_baja_no_cambia_mas_de_estado()
    {
        var nodo = NodoValido();
        nodo.DarDeBaja();

        Assert.Throws<ConflictoException>(() => nodo.DarDeBaja());
        Assert.Throws<ReglaNegocioException>(() => nodo.CambiarA(EstadoNodo.EnLinea));
    }

    [Fact]
    public void Cambiar_al_mismo_estado_no_genera_evento()
    {
        Assert.Null(NodoValido().CambiarA(EstadoNodo.SinSenal));
    }

    [Fact]
    public void Un_nodo_mal_formado_reporta_sus_problemas()
    {
        var nodo = new Nodo { Codigo = "nodo-3", Mac = "A4CF", ZonaId = 0, AjusteRssiDb = 25 };

        var problemas = nodo.Problemas();

        Assert.Equal(4, problemas.Count);   // código, MAC, zona y ajuste
        Assert.Throws<ReglaNegocioException>(nodo.Validar);
    }
}
