using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;

namespace FlowTwin.Monitoreo.Domain;

/// <summary>
/// Nivel de una zona en el gemelo. NINGUNO: sin semáforo, porque es Tránsito o Salida (RN-13) o porque
/// su aforo aún está por definir.
/// SIN_DATOS: ningún nodo En línea la cubre, así que el sistema NO SABE cuánta gente hay; mostrar
/// "0 personas, verde" sería mostrar un dato falso.
/// </summary>
public static class Nivel
{
    public const string Baja = "BAJA", Media = "MEDIA", Alta = "ALTA", Ninguno = "NINGUNO", SinDatos = "SIN_DATOS";
}

/// <summary>Pausada: fuera de servicio (ADR-25); MotivoPausa es el código del catálogo (LIMPIEZA…).</summary>
public sealed record ZonaEstado(int ZonaId, int Ocupacion, short? Aforo, string Nivel, bool Pausada, string? MotivoPausa = null);

/// <summary>
/// ZonaActual nula: no se le detecta en ninguna zona del piso ahora (sin zona, o en otro piso).
/// ZonaDesdeEn: cuándo entró a la zona actual («en Sala de espera · hace 21 min»).
/// Id: para abrir su detalle (P6); el código A-0143 se repite cada día. Estado: ver EstadoAtencion.
/// </summary>
public sealed record AtencionEstado(
    long Id, string Codigo, int? ZonaActual, string Estado, DateTime InicioEn, int EsperaMin, int ConsultaMin, DateTime? ZonaDesdeEn = null);

/// <summary>Una atención terminada hoy, para «Atendidos hoy» del gemelo.</summary>
public sealed record AtendidaHoy(long Id, string Codigo, DateTime InicioEn, DateTime FinEn, string Motivo, int EsperaMin, int ConsultaMin, int TotalMin);

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
    public static EstadoPiso Calcular(int pisoId, IReadOnlyList<ZonaSemaforo> zonas, IReadOnlyList<NodoZona> nodos,
        IReadOnlyList<AtencionEnCurso> atenciones, DateTime ahora, IReadOnlyList<PausaInfo>? pausas = null, int semaforoAmbarPct = 80)
    {
        var pausaDe = (pausas ?? []).Where(p => p.FinEn is null).ToDictionary(p => p.ZonaId);
        var tipoDe = zonas.ToDictionary(z => z.Id, z => z.Tipo);
        var zonasConDatos = nodos.Where(n => n.EnLinea).Select(n => n.ZonaId).ToHashSet();

        var porAtencion = atenciones.Select(a => Atencion(a, tipoDe, ahora)).ToList();
        var ocupacion = porAtencion.Where(a => a.ZonaActual is not null)
            .GroupBy(a => a.ZonaActual!.Value).ToDictionary(g => g.Key, g => g.Count());

        var estadoZonas = zonas.Select(z =>
        {
            var personas = ocupacion.GetValueOrDefault(z.Id);
            var pausa = pausaDe.GetValueOrDefault(z.Id);
            return new ZonaEstado(z.Id, personas, z.Aforo, NivelDe(z, personas, zonasConDatos.Contains(z.Id), semaforoAmbarPct), pausa is not null, pausa?.Motivo);
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

    /// <summary>Tiempos con la regla RN-06 (ReglaTiempos) y la zona actual, solo si es de este piso.</summary>
    private static AtencionEstado Atencion(AtencionEnCurso a, Dictionary<int, string> tipoDe, DateTime ahora)
    {
        var esServicio = EsServicio(tipoDe);
        var t = ReglaTiempos.Calcular(a.Estadias, a.InicioEn, ahora, esServicio);

        // Solo cuenta como "en una zona del piso" si su estadía abierta es en una zona de ESTE piso
        var actual = a.Estadias.LastOrDefault(e => e.SalidaEn is null && tipoDe.ContainsKey(e.ZonaId));
        var estado = ReglaTiempos.EstadoAbierta(actual, t.PrimeraConsulta, esServicio);

        return new AtencionEstado(a.Id, a.Codigo, actual?.ZonaId, estado, a.InicioEn,
            ReglaTiempos.Minutos(t.Espera), ReglaTiempos.Minutos(t.Consulta), actual?.EntradaEn);
    }

    /// <summary>
    /// Las atenciones terminadas hoy, con la MISMA regla de espera y consulta (RN-06) que las abiertas,
    /// medida hasta su fin. Solo cuenta como «Servicio» lo que es Servicio en las zonas dadas.
    /// </summary>
    public static IReadOnlyList<AtendidaHoy> Atendidas(IReadOnlyList<ZonaSemaforo> zonas, IReadOnlyList<AtencionCerrada> cerradas)
    {
        var esServicio = EsServicio(zonas.ToDictionary(z => z.Id, z => z.Tipo));
        return [.. cerradas.Select(a =>
        {
            var t = ReglaTiempos.Calcular(a.Estadias, a.InicioEn, a.FinEn, esServicio);
            return new AtendidaHoy(a.Id, a.Codigo, a.InicioEn, a.FinEn, a.Motivo,
                ReglaTiempos.Minutos(t.Espera), ReglaTiempos.Minutos(t.Consulta), ReglaTiempos.Minutos(a.FinEn - a.InicioEn));
        })];
    }

    private static Func<int, bool> EsServicio(Dictionary<int, string> tipoDe) =>
        zonaId => tipoDe.GetValueOrDefault(zonaId) == ReglaTiempos.Servicio;

    /// <summary>
    /// Semáforo (RN-13, ADR-27): verde por debajo del umbral ámbar (Parámetros, 80 % por defecto), ámbar
    /// hasta llenar el aforo y rojo al SUPERARLO (el aforo es un límite oficial). Solo donde hay aforo.
    /// </summary>
    private static string NivelDe(ZonaSemaforo z, int personas, bool tieneDatos, int ambarPct)
    {
        // Tránsito y Salida nunca tienen semáforo (RN-13). En las demás, «sin datos» va antes que
        // «aforo por definir»: un nodo caído no debe esconderse detrás de una configuración pendiente.
        if (z.Tipo is "TRANSITO" or "SALIDA") return Nivel.Ninguno;
        if (!tieneDatos) return Nivel.SinDatos;
        if (z.Aforo is not short aforo) return Nivel.Ninguno;
        var porcentaje = personas * 100.0 / aforo;
        return porcentaje > 100 ? Nivel.Alta : porcentaje >= ambarPct ? Nivel.Media : Nivel.Baja;
    }
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
            mensajes.Add(new("OcupacionZonaCambiada", new { zonaId = z.ZonaId, ocupacion = z.Ocupacion, aforo = z.Aforo, nivel = z.Nivel, pausada = z.Pausada, motivoPausa = z.MotivoPausa }));

        var atencionesAntes = antes?.Atenciones.ToDictionary(a => a.Codigo) ?? [];
        foreach (var a in ahora.Atenciones)
        {
            var previa = atencionesAntes.GetValueOrDefault(a.Codigo);
            if (previa is not null && previa.ZonaActual == a.ZonaActual && previa.Estado == a.Estado) continue;
            mensajes.Add(new("AtencionActualizada", new { id = a.Id, codigo = a.Codigo, zonaActual = a.ZonaActual, estado = a.Estado, inicioEn = a.InicioEn, esperaMin = a.EsperaMin, zonaDesdeEn = a.ZonaDesdeEn }));
        }
        // Las que ya no están: devolvieron la tarjeta (o se anuló la entrega)
        var codigosAhora = ahora.Atenciones.Select(a => a.Codigo).ToHashSet();
        foreach (var codigo in atencionesAntes.Keys.Where(c => !codigosAhora.Contains(c)))
            mensajes.Add(new("AtencionActualizada", new { id = atencionesAntes[codigo].Id, codigo, zonaActual = (int?)null, estado = EstadoAtencion.Finalizada, inicioEn = atencionesAntes[codigo].InicioEn, esperaMin = atencionesAntes[codigo].EsperaMin }));

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
