using FlowTwin.Gemelo.Application;
using FlowTwin.SharedKernel;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FlowTwin.UnitTests;

/// <summary>ADR-28: lectura de zonas.json y el almacén en disco de las maquetas.</summary>
public sealed class MaquetasTests : IDisposable
{
    private readonly string raiz = Directory.CreateTempSubdirectory("flowtwin-maquetas-").FullName;

    public void Dispose() => Directory.Delete(raiz, recursive: true);

    private sealed class Entorno(string raiz) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "FlowTwin";
        public string ContentRootPath { get; set; } = raiz;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private AlmacenMaquetasEnDisco Almacen(string? ruta) => new(Options.Create(new OpcionesMaquetas { Ruta = ruta }), new Entorno(raiz));

    [Fact]
    public void Lee_las_zonas_con_sus_rotulos()
    {
        var zonas = MaquetasServicio.LeerZonas("""{ "zonas": { "7": ["Módulo 2", "Módulo 2 – sillón 1"], "3": ["Sala de esperas"] } }""");

        Assert.Equal(["Módulo 2", "Módulo 2 – sillón 1"], zonas[7]);
        Assert.Equal(["Sala de esperas"], zonas[3]);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("""{ "otra": {} }""")]
    [InlineData("""{ "zonas": { "siete": ["Módulo 2"] } }""")]
    [InlineData("""{ "zonas": { "7": [""] } }""")]
    [InlineData("""{ "zonas": { "7": "Módulo 2" } }""")]
    public void Un_zonas_json_mal_escrito_da_un_error_claro(string json)
    {
        var e = Assert.Throws<ReglaNegocioException>(() => MaquetasServicio.LeerZonas(json));

        Assert.Equal("MAQUETA_ZONAS_INVALIDAS", e.Codigo);
    }

    [Fact]
    public async Task Sin_ruta_configurada_ningun_piso_tiene_maqueta()
    {
        var almacen = Almacen(null);

        Assert.Null(await almacen.ZonasJsonAsync(1, CancellationToken.None));
        Assert.Null(almacen.Abrir(1, ArchivoMaqueta.Modelo));
    }

    [Fact]
    public async Task Una_ruta_relativa_se_resuelve_desde_la_raiz_de_la_API_y_la_etiqueta_cambia_si_cambia_el_archivo()
    {
        var piso = Directory.CreateDirectory(Path.Combine(raiz, "maquetas", "piso-2")).FullName;
        await File.WriteAllTextAsync(Path.Combine(piso, "zonas.json"), """{ "zonas": {} }""");
        var glb = Path.Combine(piso, "maqueta.glb");
        await File.WriteAllBytesAsync(glb, [1, 2, 3]);
        var almacen = Almacen("maquetas");

        Assert.NotNull(await almacen.ZonasJsonAsync(2, CancellationToken.None));
        Assert.Null(almacen.Abrir(1, ArchivoMaqueta.Modelo));   // otro piso: no hay carpeta
        Assert.Null(almacen.Abrir(2, ArchivoMaqueta.Ambientes)); // falta el archivo
        string antes;
        using (var a = almacen.Abrir(2, ArchivoMaqueta.Modelo)!) { antes = a.Etiqueta.Tag.ToString(); Assert.Equal("model/gltf-binary", a.TipoMime); }

        await File.WriteAllBytesAsync(glb, [1, 2, 3, 4]);   // el equipo técnico instala una versión nueva
        using var despues = almacen.Abrir(2, ArchivoMaqueta.Modelo)!;

        Assert.NotEqual(antes, despues.Etiqueta.Tag.ToString());
    }
}
