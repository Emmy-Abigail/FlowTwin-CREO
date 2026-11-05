namespace FlowTwin.SharedKernel;

/// <summary>
/// Fila de un catálogo del esquema cat (Rol, EstadoTarjeta…). En C# cada catálogo es un enum,
/// y su Id es el mismo valor del enum: así el código usa EstadoTarjeta.EnUso y la base guarda 2
/// con su FK al catálogo, que además tiene el Codigo ("EN_USO") y el Nombre ("En uso").
/// </summary>
public class Catalogo<TEnum> : EntidadEditable where TEnum : struct, Enum
{
    public TEnum Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

/// <summary>Atributo para escribir el Codigo y el Nombre de cada valor del enum en un solo lugar.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class CatalogoAttribute(string codigo, string nombre) : Attribute
{
    public string Codigo { get; } = codigo;
    public string Nombre { get; } = nombre;
}

public static class CatalogoExtensiones
{
    /// <summary>El Codigo estable de un valor ("EN_USO"), el que viaja en la API.</summary>
    public static string Codigo<TEnum>(this TEnum valor) where TEnum : struct, Enum => Atributo(valor).Codigo;

    /// <summary>El texto que ve el usuario ("En uso").</summary>
    public static string Nombre<TEnum>(this TEnum valor) where TEnum : struct, Enum => Atributo(valor).Nombre;

    private static CatalogoAttribute Atributo<TEnum>(TEnum valor) where TEnum : struct, Enum =>
        typeof(TEnum).GetField(valor.ToString())!
            .GetCustomAttributes(typeof(CatalogoAttribute), false)
            .Cast<CatalogoAttribute>().Single();
}
