using FlowTwin.Identidad.Domain;

namespace FlowTwin.UnitTests;

public class IdentidadTests
{
    [Theory]
    [InlineData("Gerencia#2026")]
    [InlineData("Kp7#vR2m9Lqa")]
    public void Contrasena_que_cumple_la_politica(string clave) =>
        Assert.Empty(PoliticaContrasena.Incumplidos(clave, "rosa.vega@creo.pe", "Rosa", "Vega"));

    [Theory]
    [InlineData("rosa2026", "mayúscula")]        // ejemplo del flujo F-02
    [InlineData("ROSA#2026", "minúscula")]
    [InlineData("Clinica#Creo", "número")]
    [InlineData("Clinica2026", "símbolo")]
    [InlineData("Ab#1", "8 caracteres")]
    [InlineData("Vega#2026x", "correo ni tu nombre")]
    public void Contrasena_que_incumple_dice_que_falta(string clave, string fragmento)
    {
        var faltan = PoliticaContrasena.Incumplidos(clave, "rosa.vega@creo.pe", "Rosa", "Vega");
        Assert.Contains(faltan, f => f.Contains(fragmento));
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
