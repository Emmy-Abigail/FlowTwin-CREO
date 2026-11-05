using System.Text.Json;
using FlowTwin.SharedKernel;

namespace FlowTwin.Gemelo.Domain;

/// <summary>
/// Forma de un muro importado de AutoCAD (ADR-17): el contorno exterior y sus huecos (vanos de
/// puertas, columnas). Puntos [x, y] en metros; se guardan con la precisión del plano (mm).
/// </summary>
public sealed record PoligonoConHuecos(IReadOnlyList<double[]> Poligono, IReadOnlyList<IReadOnlyList<double[]>> Huecos)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string AJson() => JsonSerializer.Serialize(this, Json);

    public static PoligonoConHuecos DesdeJson(string json) =>
        JsonSerializer.Deserialize<PoligonoConHuecos>(json, Json)
        ?? throw new InvalidOperationException("La geometría del elemento está vacía.");
}

/// <summary>
/// gem.ElementoPlano: lo que se dibuja en el plano además de las zonas, para el 2D y el 3D.
/// Muro y Ambiente usan X1, Y1, X2, Y2 como esquinas opuestas; Puerta y Ventana, como segmento.
/// Un muro importado trae además su polígono en Geometria, que manda sobre el rectángulo.
/// Las coordenadas pueden ser algo negativas: los ejes del plano pasan por el centro del muro,
/// así que la mitad exterior de los muros perimetrales queda antes del origen.
/// </summary>
public class ElementoPlano : EntidadEditable
{
    public int Id { get; set; }
    public int PisoId { get; set; }
    public TipoElementoPlano TipoElementoPlanoId { get; set; }
    /// <summary>Solo los ambientes («Baños»).</summary>
    public string? Nombre { get; set; }
    public decimal X1 { get; set; }
    public decimal Y1 { get; set; }
    public decimal X2 { get; set; }
    public decimal Y2 { get; set; }
    /// <summary>Alto propio; si es nulo se usa el del piso.</summary>
    public decimal? AltoM { get; set; }
    /// <summary>Sin default en la base, por la misma razón que en Zona.</summary>
    public bool EnBorrador { get; set; } = true;
    public string? Geometria { get; set; }

    /// <summary>Un muro a partir de su polígono: X1..Y2 guardan el rectángulo que lo contiene.</summary>
    public static ElementoPlano Muro(int pisoId, PoligonoConHuecos forma)
    {
        var xs = forma.Poligono.Select(p => p[0]).ToList();
        var ys = forma.Poligono.Select(p => p[1]).ToList();
        return new ElementoPlano
        {
            PisoId = pisoId,
            TipoElementoPlanoId = TipoElementoPlano.Muro,
            X1 = Redondear(xs.Min()), Y1 = Redondear(ys.Min()),
            X2 = Redondear(xs.Max()), Y2 = Redondear(ys.Max()),
            Geometria = forma.AJson(),
        };
    }

    public PoligonoConHuecos? Forma() => Geometria is null ? null : PoligonoConHuecos.DesdeJson(Geometria);

    /// <summary>Lo que no cumple el elemento, para el editor. Vacía = válido.</summary>
    public IReadOnlyList<string> Problemas()
    {
        var problemas = new List<string>();
        var tipo = TipoElementoPlanoId.Nombre();
        if (TipoElementoPlanoId == TipoElementoPlano.Ambiente && string.IsNullOrWhiteSpace(Nombre))
            problemas.Add("Un ambiente necesita un nombre, por ejemplo «Baños».");
        if (Nombre is { Length: > 80 }) problemas.Add("El nombre admite hasta 80 caracteres.");
        if (AltoM is <= 0) problemas.Add("El alto debe ser mayor que 0.");

        var esRectangulo = TipoElementoPlanoId is TipoElementoPlano.Ambiente or TipoElementoPlano.Muro;
        if (esRectangulo && Geometria is null && (X2 <= X1 || Y2 <= Y1))
            problemas.Add($"Un {tipo.ToLowerInvariant()} necesita dos esquinas opuestas distintas.");
        if (!esRectangulo && X1 == X2 && Y1 == Y2)
            problemas.Add($"Una {tipo.ToLowerInvariant()} necesita dos puntos distintos.");

        if (Geometria is not null)
        {
            PoligonoConHuecos? forma = null;
            try { forma = Forma(); }
            catch (JsonException) { problemas.Add("La geometría no es un JSON válido."); }
            if (forma is not null && (forma.Poligono.Count < 3 || forma.Huecos.Any(h => h.Count < 3)))
                problemas.Add("Cada polígono y cada hueco necesita al menos 3 puntos.");
        }
        return problemas;
    }

    private static decimal Redondear(double v) => Math.Round((decimal)v, 2);
}
