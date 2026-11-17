using System.Net;

namespace FlowTwin.IntegrationTests;

/// <summary>Despliegue en un contenedor: la API sirve también la web (Web:Ruta), en el mismo dominio.</summary>
[Collection("Api")]
public sealed class WebEstaticaTests(ApiFixture api) : IDisposable
{
    private readonly string carpeta = Directory.CreateTempSubdirectory("flowtwin-web-").FullName;

    public void Dispose() => Directory.Delete(carpeta, recursive: true);

    private void Build()
    {
        File.WriteAllText(Path.Combine(carpeta, "index.html"), "<!doctype html><title>FlowTwin</title><app-root></app-root>");
        File.WriteAllText(Path.Combine(carpeta, "main-AB12_cd3.js"), "console.log('ok')");
    }

    [Fact]
    public async Task Una_ruta_de_la_app_devuelve_la_pagina_con_su_politica_de_contenido()
    {
        Build();
        using var app = api.ConAjustes(("Web:Ruta", carpeta));
        var cliente = app.CreateClient();

        var pagina = await cliente.GetAsync("/auditoria");
        var script = await cliente.GetAsync("/main-AB12_cd3.js");

        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        Assert.Contains("<app-root>", await pagina.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", pagina.Headers.CacheControl?.ToString());                // siempre la versión nueva
        var csp = pagina.Headers.GetValues("Content-Security-Policy").Single();
        var scripts = csp.Split(';').Single(d => d.Trim().StartsWith("script-src"));
        Assert.Contains("'self'", scripts);
        Assert.DoesNotContain("unsafe-inline", scripts);   // un script inyectado en la página no se ejecuta (XSS)
        Assert.Contains("max-age=31536000", script.Headers.CacheControl?.ToString());    // con huella: se guarda un año
    }

    [Fact]
    public async Task Lo_que_es_de_la_API_o_un_archivo_que_no_existe_da_404_y_no_la_pagina()
    {
        Build();
        using var app = api.ConAjustes(("Web:Ruta", carpeta));
        var cliente = app.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/v1/no-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/falta-AB12_cd3.js")).StatusCode);
        Assert.Equal("default-src 'none'; frame-ancestors 'none'",
            (await cliente.GetAsync("/api/v1/sedes")).Headers.GetValues("Content-Security-Policy").Single());   // la API sigue con la suya
    }

    [Fact]
    public async Task Sin_Web_Ruta_la_API_no_sirve_ninguna_pagina()
    {
        var r = await api.Fabrica.CreateClient().GetAsync("/auditoria");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
