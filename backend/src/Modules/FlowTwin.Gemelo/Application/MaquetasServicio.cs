using System.Text.Json;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace FlowTwin.Gemelo.Application;

/// <summary>
/// Maquetas 3D por piso (ADR-28). Las instala el equipo técnico en una carpeta del servidor, fuera del
/// repositorio y fuera de lo que se publica sin sesión: una subcarpeta «piso-{id}» con maqueta.glb,
/// ambientes.json y zonas.json. Agregar un piso = copiar su carpeta; no se programa.
/// </summary>
public sealed class OpcionesMaquetas
{
    /// <summary>Carpeta de las maquetas; relativa a la raíz de la API o absoluta. Vacía = ningún piso tiene maqueta.</summary>
    public string? Ruta { get; set; }
}

/// <summary>Los únicos archivos que se entregan: nombres fijos, así nadie puede pedir otro archivo del servidor.</summary>
public enum ArchivoMaqueta { Modelo, Ambientes }

/// <summary>Un archivo listo para enviar. Etiqueta (ETag) cambia si el archivo cambia: el navegador reusa su copia si no.</summary>
public sealed record ContenidoMaqueta(Stream Contenido, string TipoMime, DateTimeOffset Modificado, EntityTagHeaderValue Etiqueta) : IDisposable
{
    public void Dispose() => Contenido.Dispose();
}

/// <summary>
/// Dónde viven las maquetas. Hoy, en disco; si mañana van a un almacenamiento en la nube, solo cambia esta pieza.
/// </summary>
public interface IAlmacenMaquetas
{
    /// <summary>El texto de zonas.json del piso, o null si el piso no tiene maqueta.</summary>
    Task<string?> ZonasJsonAsync(int pisoId, CancellationToken ct);

    /// <summary>El archivo pedido, o null si no existe.</summary>
    ContenidoMaqueta? Abrir(int pisoId, ArchivoMaqueta archivo);
}

public sealed class AlmacenMaquetasEnDisco(IOptions<OpcionesMaquetas> opciones, IHostEnvironment entorno) : IAlmacenMaquetas
{
    private readonly string? raiz = string.IsNullOrWhiteSpace(opciones.Value.Ruta)
        ? null
        : Path.GetFullPath(Path.Combine(entorno.ContentRootPath, opciones.Value.Ruta));

    public async Task<string?> ZonasJsonAsync(int pisoId, CancellationToken ct)
    {
        var ruta = Ruta(pisoId, "zonas.json");
        return ruta is not null && File.Exists(ruta) ? await File.ReadAllTextAsync(ruta, ct) : null;
    }

    public ContenidoMaqueta? Abrir(int pisoId, ArchivoMaqueta archivo)
    {
        var (nombre, mime) = archivo switch
        {
            ArchivoMaqueta.Modelo => ("maqueta.glb", "model/gltf-binary"),
            _ => ("ambientes.json", "application/json"),
        };
        var ruta = Ruta(pisoId, nombre);
        if (ruta is null) return null;
        var info = new FileInfo(ruta);
        if (!info.Exists) return null;
        // Tamaño y fecha bastan para saber si cambió, sin leer los 4,7 MB para un hash
        var etiqueta = new EntityTagHeaderValue($"\"{info.Length:x}-{info.LastWriteTimeUtc.Ticks:x}\"");
        return new ContenidoMaqueta(info.OpenRead(), mime, info.LastWriteTimeUtc, etiqueta);
    }

    /// <summary>
    /// El id es un entero y los nombres son fijos, así que la ruta no puede salir de la carpeta; igual se
    /// comprueba (defensa en profundidad) por si la configuración trae algo raro.
    /// </summary>
    private string? Ruta(int pisoId, string nombre)
    {
        if (raiz is null || pisoId <= 0) return null;
        var ruta = Path.GetFullPath(Path.Combine(raiz, $"piso-{pisoId}", nombre));
        return ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? ruta : null;
    }
}

/// <summary>GET /pisos/{id}/maqueta: qué ambientes de la maqueta pinta cada zona (zonaId → rótulos del plano).</summary>
public sealed record MaquetaDto(int PisoId, IReadOnlyDictionary<int, string[]> Zonas);

public sealed class MaquetasServicio(IGemeloDb db, IUsuarioActual usuario, IAlmacenMaquetas almacen)
{
    public async Task<MaquetaDto> InfoAsync(int pisoId, CancellationToken ct)
    {
        await ValidarAccesoAsync(pisoId, ct);
        var json = await almacen.ZonasJsonAsync(pisoId, ct) ?? throw SinMaqueta(pisoId);

        // Solo las zonas publicadas de ESTE piso: un zonas.json copiado de otro piso no pinta zonas ajenas
        var zonasDelPiso = (await db.Zonas.Where(z => z.PisoId == pisoId && !z.EnBorrador).Select(z => z.Id).ToListAsync(ct)).ToHashSet();
        var zonas = LeerZonas(json).Where(kv => zonasDelPiso.Contains(kv.Key)).ToDictionary();
        return new MaquetaDto(pisoId, zonas);
    }

    public async Task<ContenidoMaqueta> ArchivoAsync(int pisoId, ArchivoMaqueta archivo, CancellationToken ct)
    {
        await ValidarAccesoAsync(pisoId, ct);
        return almacen.Abrir(pisoId, archivo) ?? throw SinMaqueta(pisoId);
    }

    /// <summary>
    /// zonas.json: { "zonas": { "7": ["Módulo 2", "Módulo 2 – sillón 1"] } }. Lo escribe una persona: si está
    /// mal, se dice qué falla en vez de mostrar un gemelo sin colores sin explicación.
    /// </summary>
    public static Dictionary<int, string[]> LeerZonas(string json)
    {
        Dictionary<string, string[]>? crudo;
        try
        {
            crudo = JsonDocument.Parse(json).RootElement.GetProperty("zonas").Deserialize<Dictionary<string, string[]>>();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ReglaNegocioException("MAQUETA_ZONAS_INVALIDAS", "El archivo zonas.json de la maqueta no tiene el formato esperado.");
        }
        var zonas = new Dictionary<int, string[]>();
        foreach (var (clave, rotulos) in crudo ?? [])
        {
            if (!int.TryParse(clave, out var zonaId) || rotulos is null || rotulos.Any(string.IsNullOrWhiteSpace))
                throw new ReglaNegocioException("MAQUETA_ZONAS_INVALIDAS", $"En zonas.json, la zona «{clave}» no es válida.");
            zonas[zonaId] = rotulos;
        }
        return zonas;
    }

    /// <summary>La misma regla que la geometría: piso publicado y asignado al usuario (Admin ve todos, RN-24).</summary>
    private async Task ValidarAccesoAsync(int pisoId, CancellationToken ct)
    {
        if (!await db.Pisos.AnyAsync(p => p.Id == pisoId && p.Publicado, ct))
            throw new NoEncontradoException($"No existe el piso {pisoId} o aún no está publicado.");
        if (!usuario.PuedeVerPiso(pisoId))
            throw new SinPermisoException("No tienes asignado este piso.");
    }

    private static NoEncontradoException SinMaqueta(int pisoId) =>
        new($"El piso {pisoId} todavía no tiene maqueta 3D instalada.", "SIN_MAQUETA");
}
