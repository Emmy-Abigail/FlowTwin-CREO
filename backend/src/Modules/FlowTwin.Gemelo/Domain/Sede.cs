using FlowTwin.SharedKernel;

namespace FlowTwin.Gemelo.Domain;

/// <summary>Catálogo cat.TipoZona: define cómo cuenta el tiempo en las métricas (RN-06, RN-13).</summary>
public enum TipoZona
{
    [Catalogo("INGRESO", "Ingreso")] Ingreso = 1,
    [Catalogo("ESPERA", "Espera")] Espera = 2,
    [Catalogo("SERVICIO", "Servicio")] Servicio = 3,
    [Catalogo("TRANSITO", "Tránsito")] Transito = 4,
    [Catalogo("SALIDA", "Salida")] Salida = 5,
}

/// <summary>Catálogo cat.TipoElementoPlano: lo que se dibuja en el plano además de las zonas.</summary>
public enum TipoElementoPlano
{
    [Catalogo("MURO", "Muro")] Muro = 1,
    [Catalogo("PUERTA", "Puerta")] Puerta = 2,
    [Catalogo("VENTANA", "Ventana")] Ventana = 3,
    [Catalogo("AMBIENTE", "Ambiente")] Ambiente = 4,
}

/// <summary>gem.Sede. El prefijo arma el código anónimo de cada atención (A-0143).</summary>
public class Sede : EntidadEditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Direccion { get; set; }
    public char PrefijoAtencion { get; set; } = 'A';
}

/// <summary>gem.Piso. Sus zonas están en gem.Zona; los muros y versiones publicadas llegan después.</summary>
public class Piso : EntidadEditable
{
    public int Id { get; set; }
    public int SedeId { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Orden vertical; los sótanos son negativos.</summary>
    public short Nivel { get; set; }
    public string? PlanoRuta { get; set; }
    public decimal? AnchoM { get; set; }
    public decimal? FondoM { get; set; }
    public decimal AltoMuroM { get; set; } = 2.70m;
    public bool Publicado { get; set; }
    public DateTime? PublicadoEn { get; set; }
}
