using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowTwin.IntegrationTests;

/// <summary>Informe de seguridad: límite de peticiones en toda la API, tamaño máximo y cabeceras de seguridad.</summary>
[Collection("Api")]
public class ProteccionTests(ApiFixture api)
{
    private static async Task<HttpClient> ClienteAsync(WebApplicationFactory<Program> app, string correo)
    {
        var c = app.CreateClient();
        var r = await c.PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = "demo123" });
        r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        return c;
    }

    [Fact]
    public async Task Quien_se_pasa_del_limite_recibe_429_y_los_demas_siguen_atendidos()
    {
        using var app = api.ConAjustes(("LimitePeticiones:ApiRafaga", "5"), ("LimitePeticiones:ApiPorMinuto", "6"));
        var abusador = await ClienteAsync(app, "gerencia@creo.pe");
        var otro = await ClienteAsync(app, "admin@creo.pe");

        var respuestas = new List<HttpResponseMessage>();
        for (var i = 0; i < 12; i++) respuestas.Add(await abusador.GetAsync("/api/v1/sedes"));
        var delOtro = await otro.GetAsync("/api/v1/sedes");

        var rechazada = respuestas.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.True(respuestas.Count(r => r.StatusCode == HttpStatusCode.OK) <= 6);   // la ráfaga, y nada más
        Assert.NotNull(rechazada.Headers.RetryAfter);                                  // le dice cuánto esperar
        Assert.Equal(HttpStatusCode.OK, delOtro.StatusCode);                           // el límite es por usuario
    }

    [Fact]
    public async Task Sin_sesion_tambien_se_limita_por_IP()
    {
        using var app = api.ConAjustes(("LimitePeticiones:ApiRafaga", "3"), ("LimitePeticiones:ApiPorMinuto", "6"));
        var anonimo = app.CreateClient();

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++) codigos.Add((await anonimo.GetAsync("/api/v1/sedes")).StatusCode);

        Assert.Contains(HttpStatusCode.Unauthorized, codigos);
        Assert.Contains(HttpStatusCode.TooManyRequests, codigos);   // el 401 no libra de contar
    }

    [Fact]
    public async Task Una_peticion_demasiado_grande_se_rechaza_sin_leerla()
    {
        using var app = api.ConAjustes(("LimitePeticiones:CuerpoMaximoBytes", "1024"));
        var cuerpo = new StringContent($$"""{"correo":"admin@creo.pe","clave":"{{new string('x', 4000)}}"}""", Encoding.UTF8, "application/json");

        var r = await app.CreateClient().PostAsync("/api/v1/auth/login", cuerpo);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
    }

    [Fact]
    public async Task Las_respuestas_llevan_las_cabeceras_de_seguridad_y_no_dicen_que_servidor_es()
    {
        var admin = await api.ClienteDeAsync("admin@creo.pe");

        var r = await admin.GetAsync("/api/v1/sedes");
        var sinSesion = await api.Fabrica.CreateClient().GetAsync("/api/v1/sedes");

        foreach (var respuesta in new[] { r, sinSesion })   // también en los errores
        {
            Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", respuesta.Headers.GetValues("Referrer-Policy").Single());
            Assert.Equal("same-origin", respuesta.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
            Assert.Contains("frame-ancestors 'none'", respuesta.Headers.GetValues("Content-Security-Policy").Single());
            Assert.False(respuesta.Headers.Contains("Server"));
        }
        Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
    }
}
