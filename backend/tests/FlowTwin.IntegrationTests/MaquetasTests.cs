using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowTwin.IntegrationTests;

/// <summary>
/// ADR-28: la maqueta 3D se entrega desde una carpeta del servidor, con sesión y permiso por piso.
/// Cada prueba usa su propia carpeta temporal con archivos falsos (no los de la clínica).
/// </summary>
[Collection("Api")]
public sealed class MaquetasTests(ApiFixture api) : IDisposable
{
    private readonly string carpeta = Directory.CreateTempSubdirectory("flowtwin-maquetas-").FullName;

    public void Dispose() => Directory.Delete(carpeta, recursive: true);

    private static readonly byte[] GlbFalso = [0x67, 0x6C, 0x54, 0x46, 2, 0, 0, 0];   // «glTF» v2: basta para la prueba

    private void InstalarPiso1(string zonasJson = """{ "zonas": { "7": ["Módulo 2", "Módulo 2 – sillón 1"], "999": ["De otro piso"] } }""")
    {
        var piso = Directory.CreateDirectory(Path.Combine(carpeta, "piso-1")).FullName;
        File.WriteAllBytes(Path.Combine(piso, "maqueta.glb"), GlbFalso);
        File.WriteAllText(Path.Combine(piso, "ambientes.json"), """{ "ambientes": [] }""");
        File.WriteAllText(Path.Combine(piso, "zonas.json"), zonasJson);
    }

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
    public async Task Entrega_las_zonas_del_piso_y_el_modelo_y_reusa_la_copia_si_no_cambio()
    {
        InstalarPiso1();
        using var app = api.ConAjustes(("Maquetas:Ruta", carpeta));
        var operador = await ClienteAsync(app, "operador@creo.pe");

        var info = await (await operador.GetAsync("/api/v1/pisos/1/maqueta")).Content.ReadFromJsonAsync<JsonElement>();
        var modelo = await operador.GetAsync("/api/v1/pisos/1/maqueta/modelo.glb");

        var zonas = info.GetProperty("zonas");
        Assert.Equal(["Módulo 2", "Módulo 2 – sillón 1"], zonas.GetProperty("7").EnumerateArray().Select(r => r.GetString()));
        Assert.False(zonas.TryGetProperty("999", out _));   // una zona que no es de este piso no se pinta
        Assert.Equal(HttpStatusCode.OK, modelo.StatusCode);
        Assert.Equal("model/gltf-binary", modelo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(GlbFalso, await modelo.Content.ReadAsByteArrayAsync());
        Assert.Contains("private", modelo.Headers.CacheControl?.ToString());

        // Segunda visita: el navegador manda el ETag que tiene y no se reenvía el archivo
        using var otraVez = new HttpRequestMessage(HttpMethod.Get, "/api/v1/pisos/1/maqueta/modelo.glb");
        otraVez.Headers.IfNoneMatch.Add(modelo.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await operador.SendAsync(otraVez)).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_se_descarga_nada()
    {
        InstalarPiso1();
        using var app = api.ConAjustes(("Maquetas:Ruta", carpeta));

        var r = await app.CreateClient().GetAsync("/api/v1/pisos/1/maqueta/modelo.glb");

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Un_piso_sin_maqueta_responde_SIN_MAQUETA_y_uno_no_asignado_SIN_PERMISO()
    {
        using var app = api.ConAjustes(("Maquetas:Ruta", carpeta));   // carpeta vacía
        var admin = await ClienteAsync(app, "admin@creo.pe");
        var operador = await ClienteAsync(app, "operador@creo.pe");
        // Un piso publicado que el Operador no tiene asignado
        var sede = await (await admin.PostAsJsonAsync("/api/v1/configuracion/sedes", new { nombre = "Sede Maquetas (prueba)", direccion = (string?)null, prefijoAtencion = 'M' }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var piso = await (await admin.PostAsJsonAsync($"/api/v1/configuracion/sedes/{sede.GetProperty("id").GetInt32()}/pisos", new { nombre = "Piso 1", nivel = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var pisoId = piso.GetProperty("id").GetInt32();
        await admin.PostAsJsonAsync($"/api/v1/configuracion/pisos/{pisoId}/zonas", new { nombre = "Sala", tipo = "ESPERA", aforo = (int?)null });
        await admin.PostAsync($"/api/v1/configuracion/pisos/{pisoId}/publicar", null);

        var sinMaqueta = await operador.GetAsync("/api/v1/pisos/1/maqueta");
        var ajeno = await operador.GetAsync($"/api/v1/pisos/{pisoId}/maqueta/modelo.glb");

        Assert.Equal(HttpStatusCode.NotFound, sinMaqueta.StatusCode);
        Assert.Equal("SIN_MAQUETA", (await sinMaqueta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, ajeno.StatusCode);
    }

    [Fact]
    public async Task Un_zonas_json_mal_escrito_se_explica()
    {
        InstalarPiso1("""{ "zonas": { "siete": ["Módulo 2"] } }""");
        using var app = api.ConAjustes(("Maquetas:Ruta", carpeta));

        var r = await (await ClienteAsync(app, "operador@creo.pe")).GetAsync("/api/v1/pisos/1/maqueta");

        Assert.Equal("MAQUETA_ZONAS_INVALIDAS", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
    }
}
