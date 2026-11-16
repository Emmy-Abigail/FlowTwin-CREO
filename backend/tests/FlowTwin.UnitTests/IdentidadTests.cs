using FlowTwin.Identidad.Domain;

namespace FlowTwin.UnitTests;

public class IdentidadTests
{
    [Theory]
    [InlineData("Gerencia#2026")]
    [InlineData("Kp7#vR2m9Lqa")]
    [InlineData("el gemelo de la sala azul")]     // una frase: sin mayúsculas, números ni símbolos, y es válida
    [InlineData("riñón y corazón en paz")]          // tildes y ñ cuentan como cualquier letra
    public void Contrasena_que_cumple_la_politica(string clave) =>
        Assert.Empty(PoliticaContrasena.Incumplidos(clave, "rosa.vega@creo.pe", "Rosa", "Vega"));

    [Theory]
    [InlineData("Ab#1x", "12 caracteres")]
    [InlineData("Vega en la clinica 26", "correo ni tu nombre")]
    [InlineData("aaaaaaaaaaaaa", "repetitiva")]
    [InlineData("121212121212", "repetitiva")]
    [InlineData("qwerty123456", "conocida")]                 // está en la lista de contraseñas filtradas
    [InlineData("QWERTY123456", "conocida")]                 // sin importar mayúsculas
    [InlineData("FlowTwin2026!", "conocida")]                // solo una palabra del sistema con números
    [InlineData("Contraseña#2026", "conocida")]
    public void Contrasena_que_incumple_dice_que_falta(string clave, string fragmento)
    {
        var faltan = PoliticaContrasena.Incumplidos(clave, "rosa.vega@creo.pe", "Rosa", "Vega");
        Assert.Contains(faltan, f => f.Contains(fragmento));
    }

    [Fact]
    public void Ya_no_se_exige_combinar_mayusculas_numeros_y_simbolos_ASVS()
    {
        Assert.Empty(PoliticaContrasena.Incumplidos("tengo una tortuga verde", "x@creo.pe", "", ""));
        Assert.Empty(PoliticaContrasena.Incumplidos("TENGO UNA TORTUGA VERDE", "x@creo.pe", "", ""));
    }

    [Fact]
    public void Una_frase_puede_mencionar_la_clinica_si_no_es_solo_eso()
    {
        Assert.Empty(PoliticaContrasena.Incumplidos("trabajo en creo desde marzo", "x@creo.pe", "", ""));
    }

    [Fact]
    public void Hay_un_tope_para_no_cargar_el_servidor_con_textos_enormes()
    {
        Assert.Contains(PoliticaContrasena.Incumplidos(new string('x', 120) + "abcdefghij", "x@creo.pe", "", ""), f => f.Contains("128"));
    }

    [Fact]
    public void La_temporal_generada_siempre_cumple_la_politica()
    {
        for (var i = 0; i < 200; i++)
        {
            var temporal = PoliticaContrasena.GenerarTemporal();
            Assert.Equal(12, temporal.Length);
            Assert.Empty(PoliticaContrasena.Incumplidos(temporal, "x@creo.pe", "", ""));
        }
    }

    [Fact]
    public void Cinco_intentos_fallidos_bloquean_15_minutos()
    {
        var ahora = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var u = new Usuario();

        for (var i = 0; i < 4; i++) u.RegistrarIntentoFallido(ahora, TimeSpan.FromMinutes(15));
        Assert.False(u.EstaBloqueado(ahora));

        u.RegistrarIntentoFallido(ahora, TimeSpan.FromMinutes(15));
        Assert.True(u.EstaBloqueado(ahora.AddMinutes(14)));
        Assert.False(u.EstaBloqueado(ahora.AddMinutes(16)));
    }
}
