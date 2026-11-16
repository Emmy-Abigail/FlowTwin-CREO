using FlowTwin.Atenciones.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.Monitoreo.Domain;

/// <summary>
/// Sección «Alertas» de appsettings (ADR-25). Valores por defecto tomados de la práctica de sistemas RTLS
/// en clínicas (avisos de espera a los 20, 40 y 60 min); la clínica no definió los suyos.
/// </summary>
public sealed class OpcionesAlertas
{
    public int EsperaAvisoMin { get; set; } = 20;
    public int EsperaAltaMin { get; set; } = 40;
    public int EsperaCriticaMin { get; set; } = 60;
    /// <summary>Sin lecturas por este tiempo: el paciente pudo irse sin devolver la tarjeta (o estar en una zona sin nodo).</summary>
    public int SinLecturasMin { get; set; } = 30;
}

public static class NivelAlerta
{
    public const string Aviso = "AVISO", Alta = "ALTA", Critica = "CRITICA";
}

/// <summary>
/// Una situación que pide atención AHORA. Tipo: código del catálogo cat.TipoAlerta (ESPERA_LARGA,
/// TARJETA_SIN_DEVOLVER —sin lecturas: puede haberse ido sin devolverla—, NODO_SIN_SENAL). No se guarda: se calcula del estado actual y desaparece sola
/// cuando se resuelve (ADR-25). Clave: identifica la alerta y su nivel; si cambia (por ejemplo, la espera
/// pasa de 40 a 60 min) es una alerta nueva y suena. Desde: desde cuándo (la pantalla calcula los minutos).
/// </summary>
public sealed record AlertaPiso(string Clave, string Tipo, string Nivel, DateTime Desde,
    long? AtencionId = null, string? Codigo = null, int? ZonaId = null, string? Nodo = null);

/// <summary>Las alertas de un piso. Es PURO: se prueba sin base ni reloj del sistema.</summary>
public static class CalculoAlertas
{
    private static readonly string Espera = TipoAlerta.EsperaLarga.Codigo(), SinDevolver = TipoAlerta.TarjetaSinDevolver.Codigo(),
        NodoSinSenal = TipoAlerta.NodoSinSenal.Codigo();

    public static IReadOnlyList<AlertaPiso> Calcular(EstadoPiso estado, IReadOnlyList<AtencionEnCurso> atenciones, OpcionesAlertas o, DateTime ahora)
    {
        var alertas = new List<AlertaPiso>();

        // Sin lecturas: la última vez que un nodo la vio (o la entrega, si nunca se la vio). Va primero porque
        // es la que pide una acción: si no se le detecta hace media hora, «lleva N min esperando» solo es ruido
        var sinLecturas = new HashSet<long>();
        foreach (var a in atenciones)
        {
            var visto = a.UltimaLecturaEn ?? a.InicioEn;
            if (ahora - visto < TimeSpan.FromMinutes(o.SinLecturasMin)) continue;
            sinLecturas.Add(a.Id);
            var zona = a.Estadias.LastOrDefault()?.ZonaId;
            alertas.Add(new AlertaPiso($"{SinDevolver}:{a.Id}", SinDevolver, NivelAlerta.Alta, visto, a.Id, a.Codigo, zona));
        }

        foreach (var a in estado.Atenciones.Where(a => a.Estado == EstadoAtencion.EnEspera && a.EsperaMin >= o.EsperaAvisoMin && !sinLecturas.Contains(a.Id)))
        {
            var nivel = a.EsperaMin >= o.EsperaCriticaMin ? NivelAlerta.Critica : a.EsperaMin >= o.EsperaAltaMin ? NivelAlerta.Alta : NivelAlerta.Aviso;
            alertas.Add(new AlertaPiso($"{Espera}:{a.Id}:{nivel}", Espera, nivel, a.InicioEn, a.Id, a.Codigo, a.ZonaActual));
        }

        foreach (var n in estado.Nodos.Where(n => n.Estado != "EN_LINEA"))
            alertas.Add(new AlertaPiso($"{NodoSinSenal}:{n.NodoId}", NodoSinSenal, NivelAlerta.Alta,
                n.UltimoLatidoEn ?? ahora, ZonaId: n.ZonaId, Nodo: n.Codigo));

        // Lo más urgente primero; dentro de cada nivel, lo más antiguo
        return [.. alertas.OrderBy(a => a.Nivel switch { NivelAlerta.Critica => 0, NivelAlerta.Alta => 1, _ => 2 }).ThenBy(a => a.Desde)];
    }
}
