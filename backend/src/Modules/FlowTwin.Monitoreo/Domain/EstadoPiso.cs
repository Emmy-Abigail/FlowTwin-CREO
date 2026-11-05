using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;

namespace FlowTwin.Monitoreo.Domain;

/// <summary>
/// Nivel de una zona en el gemelo. NINGUNO: Tránsito o Salida, sin aforo ni color (RN-13).
/// SIN_DATOS: ningún nodo En línea la cubre, así que el sistema NO SABE cuánta gente hay; mostrar
/// "0 personas, verde" sería mostrar un dato falso.
/// </summary>
public static class Nivel
{
    public const string Baja = "BAJA", Media = "MEDIA", Alta = "ALTA", Ninguno = "NINGUNO", SinDatos = "SIN_DATOS";
}

/// <summary>EN_ESPERA: aún no entra a una zona Servicio. EN_ATENCION: está en una. ATENDIDO: ya pasó por una y salió.</summary>
public static class EstadoAtencion
{
    public const string EnEspera = "EN_ESPERA", EnAtencion = "EN_ATENCION", Atendido = "ATENDIDO";
}

public sealed record ZonaEstado(int ZonaId, int Ocupacion, short? Aforo, string Nivel, bool Pausada);

/// <summary>ZonaActual nula: no se le detecta en ninguna zona del piso ahora (sin zona, o en otro piso).</summary>
public sealed record AtencionEstado(string Codigo, int? ZonaActual, string Estado, DateTime InicioEn, int EsperaMin, int ConsultaMin);

/// <summary>EsperaPromMin y ConsultaPromMin son nulos si todavía no hay con qué calcularlos (no un 0 que engañe).</summary>
public sealed record KpisPiso(int EnClinica, int EnEspera, int? EsperaPromMin, int? ConsultaPromMin);

public sealed record NodoEstado(int NodoId, string Codigo, int ZonaId, string Estado, DateTime? UltimoLatidoEn);

/// <summary>GET /pisos/{id}/estado: la foto del piso en un instante (F-11).</summary>
public sealed record EstadoPiso(int PisoId, DateTime GeneradoEn, IReadOnlyList<ZonaEstado> Zonas,
    IReadOnlyList<AtencionEstado> Atenciones, KpisPiso Kpis, IReadOnlyList<NodoEstado> Nodos);

/// <summary>
/// Calcula el estado de un piso. Es PURO (sin base ni reloj del sistema): las reglas se prueban con
/// precisión y la misma foto sirve para el GET y para comparar qué cambió (notificador SignalR).
/// </summary>
public static class CalculoEstado
{
    private const string Servicio = "SERVICIO";

    public static EstadoPiso Calcular(int pisoId, IReadOnlyList<ZonaSemaforo> zonas, IReadOnlyList<NodoZona> nodos,
        IReadOnlyList<AtencionEnCurso> atenciones, DateTime ahora)
    {
        var tipoDe = zonas.ToDictionary(z => z.Id, z => z.Tipo);
        var zonasConDatos = nodos.Where(n => n.EnLinea).Select(n => n.ZonaId).ToHashSet();

        var porAtencion = atenciones.Select(a => Atencion(a, tipoDe, ahora)).ToList();
        var ocupacion = porAtencion.Where(a => a.ZonaActual is not null)
            .GroupBy(a => a.ZonaActual!.Value).ToDictionary(g => g.Key, g => g.Count());

        var estadoZonas = zonas.Select(z =>
        {
            var personas = ocupacion.GetValueOrDefault(z.Id);
            return new ZonaEstado(z.Id, personas, z.Aforo, NivelDe(z, personas, zonasConDatos.Contains(z.Id)), Pausada: false);
        }).ToList();

        var esperas = porAtencion.Select(a => a.EsperaMin).ToList();
        var consultas = porAtencion.Where(a => a.Estado != EstadoAtencion.EnEspera).Select(a => a.ConsultaMin).ToList();
        var kpis = new KpisPiso(
            EnClinica: porAtencion.Count,
            EnEspera: porAtencion.Count(a => a.Estado == EstadoAtencion.EnEspera),
            EsperaPromMin: esperas.Count == 0 ? null : (int)Math.Round(esperas.Average()),
            ConsultaPromMin: consultas.Count == 0 ? null : (int)Math.Round(consultas.Average()));

        var estadoNodos = nodos.Where(n => tipoDe.ContainsKey(n.ZonaId))
            .Select(n => new NodoEstado(n.Id, n.Codigo, n.ZonaId, n.Estado, n.UltimoLatidoEn)).ToList();

        return new EstadoPiso(pisoId, ahora, estadoZonas, porAtencion, kpis, estadoNodos);
    }

    /// <summary>
    /// RN-06: espera = desde la entrega hasta la PRIMERA entrada a una zona Servicio (si aún no entra,
    /// sigue corriendo); consulta = tiempo total en zonas Servicio.
    /// </summary>
    private static AtencionEstado Atencion(AtencionEnCurso a, Dictionary<int, string> tipoDe, DateTime ahora)
    {
        var enServicio = a.Estadias.Where(e => tipoDe.GetValueOrDefault(e.ZonaId) == Servicio).ToList();
        var primeraConsulta = enServicio.Select(e => (DateTime?)e.EntradaEn).Min();
        var espera = (primeraConsulta ?? ahora) - a.InicioEn;
        var consulta = enServicio.Aggregate(TimeSpan.Zero, (t, e) => t + ((e.SalidaEn ?? ahora) - e.EntradaEn));

        // Solo cuenta como "en una zona del piso" si su estadía abierta es en una zona de ESTE piso
        var actual = a.Estadias.LastOrDefault(e => e.SalidaEn is null && tipoDe.ContainsKey(e.ZonaId));
        var estado = actual is not null && tipoDe[actual.ZonaId] == Servicio ? EstadoAtencion.EnAtencion
            : primeraConsulta is null ? EstadoAtencion.EnEspera
            : EstadoAtencion.Atendido;

        return new AtencionEstado(a.Codigo, actual?.ZonaId, estado, a.InicioEn, Minutos(espera), Minutos(consulta));
    }

    private static string NivelDe(ZonaSemaforo z, int personas, bool tieneDatos)
    {
        if (z.Aforo is not short aforo || z.UmbralMediaPct is null || z.UmbralAltaPct is null) return Nivel.Ninguno;
        if (!tieneDatos) return Nivel.SinDatos;
        var porcentaje = personas * 100.0 / aforo;
        return porcentaje < z.UmbralMediaPct ? Nivel.Baja : porcentaje < z.UmbralAltaPct ? Nivel.Media : Nivel.Alta;
    }

    private static int Minutos(TimeSpan t) => (int)Math.Floor(Math.Max(0, t.TotalMinutes));
}

/// <summary>Un mensaje para el hub: el método que recibe el navegador y su contenido.</summary>
public sealed record MensajeGemelo(string Metodo, object Contenido);

/// <summary>
/// Compara dos fotos del piso y devuelve solo lo que cambió (sección "Hub SignalR"). La espera de cada
/// paciente avanza sola cada minuto y el navegador puede calcularla con InicioEn: por eso no dispara
/// mensajes, solo los cambios de zona o de estado.
/// </summary>
public static class Diferencias
{
    public static IReadOnlyList<MensajeGemelo> Entre(EstadoPiso? antes, EstadoPiso ahora)
    {
        var mensajes = new List<MensajeGemelo>();

        var zonasAntes = antes?.Zonas.ToDictionary(z => z.ZonaId) ?? [];
        foreach (var z in ahora.Zonas.Where(z => zonasAntes.GetValueOrDefault(z.ZonaId) != z))
            mensajes.Add(new("OcupacionZonaCambiada", new { zonaId = z.ZonaId, ocupacion = z.Ocupacion, aforo = z.Aforo, nivel = z.Nivel, pausada = z.Pausada }));

        var atencionesAntes = antes?.Atenciones.ToDictionary(a => a.Codigo) ?? [];
        foreach (var a in ahora.Atenciones)
        {
            var previa = atencionesAntes.GetValueOrDefault(a.Codigo);
            if (previa is not null && previa.ZonaActual == a.ZonaActual && previa.Estado == a.Estado) continue;
            mensajes.Add(new("AtencionActualizada", new { codigo = a.Codigo, zonaActual = a.ZonaActual, estado = a.Estado, inicioEn = a.InicioEn, esperaMin = a.EsperaMin }));
        }
        // Las que ya no están: devolvieron la tarjeta (o se anuló la entrega)
        var codigosAhora = ahora.Atenciones.Select(a => a.Codigo).ToHashSet();
        foreach (var codigo in atencionesAntes.Keys.Where(c => !codigosAhora.Contains(c)))
            mensajes.Add(new("AtencionActualizada", new { codigo, zonaActual = (int?)null, estado = "FINALIZADA", inicioEn = atencionesAntes[codigo].InicioEn, esperaMin = atencionesAntes[codigo].EsperaMin }));

        if (antes?.Kpis != ahora.Kpis)
            mensajes.Add(new("KpisActualizados", new
            {
                enClinica = ahora.Kpis.EnClinica, enEspera = ahora.Kpis.EnEspera,
                esperaPromMin = ahora.Kpis.EsperaPromMin, consultaPromMin = ahora.Kpis.ConsultaPromMin,
            }));

        var nodosAntes = antes?.Nodos.ToDictionary(n => n.NodoId) ?? [];
        foreach (var n in ahora.Nodos.Where(n => nodosAntes.GetValueOrDefault(n.NodoId)?.Estado != n.Estado))
            mensajes.Add(new("NodoEstadoCambiado", new { nodoId = n.NodoId, codigo = n.Codigo, estado = n.Estado, ultimoLatidoEn = n.UltimoLatidoEn }));

        return mensajes;
    }
}
