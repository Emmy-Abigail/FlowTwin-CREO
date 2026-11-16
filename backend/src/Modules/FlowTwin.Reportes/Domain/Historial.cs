using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.Reportes.Domain;

/// <summary>
/// Periodo del historial: días de Lima, ambos incluidos. Máximo 31 días por consulta: alcanza para comparar
/// las semanas de un mes y, con los volúmenes del piloto, se calcula al momento sin tablas resumidas.
/// </summary>
public sealed record Periodo(DateOnly Desde, DateOnly Hasta)
{
    public const int MaximoDias = 31;

    /// <summary>Hasta 7 días se ve cada fecha; con más, el promedio de cada día de la semana.</summary>
    public const int MaximoDiasPorFecha = 7;

    public int Dias => Hasta.DayNumber - Desde.DayNumber + 1;

    public IEnumerable<DateOnly> Fechas => Enumerable.Range(0, Dias).Select(Desde.AddDays);

    /// <summary>400 si el periodo está al revés, es más largo que 31 días o empieza en el futuro.</summary>
    public static Periodo Validar(DateOnly desde, DateOnly hasta, DateOnly hoy)
    {
        if (hasta < desde)
            throw new ReglaNegocioException("PERIODO_INVALIDO", "La fecha final no puede ser anterior a la inicial.");
        if (hasta > hoy)
            throw new ReglaNegocioException("PERIODO_INVALIDO", "El historial llega hasta hoy: la fecha final no puede ser futura.");
        var p = new Periodo(desde, hasta);
        if (p.Dias > MaximoDias)
            throw new ReglaNegocioException("PERIODO_INVALIDO", $"El periodo puede tener como máximo {MaximoDias} días.");
        return p;
    }
}

/// <summary>Una fila de la tabla de atenciones (y del CSV). Los minutos siguen la misma regla que P6 (RN-06).</summary>
public sealed record FilaHistorial(
    long Id, string Codigo, DateOnly Fecha, DateTime InicioEn, DateTime? FinEn, string Estado, string? Motivo,
    int EsperaMin, int? ConsultaMin, int TotalMin)
{
    public bool Terminada => FinEn is not null;
}

/// <summary>
/// Las tres cifras del periodo. Atenciones: todas las no anuladas (RN-04), abiertas o cerradas. Los tiempos
/// salen SOLO de las terminadas, porque las abiertas aún no tienen su tiempo final. La espera, además, solo
/// de las que llegaron a consulta: si nunca entró, no hay «espera hasta ser atendido» que medir.
/// Mediana y percentil 90 en vez del promedio: un solo caso extremo no los mueve.
/// </summary>
public sealed record ResumenHistorial(
    int Atenciones, int Terminadas,
    int ConEspera, int? EsperaMediana, int? EsperaP90,
    int? TotalMediana, int? TotalP90);

/// <summary>Una zona que se puede elegir en el mapa. ConNodo: tiene al menos un nodo que no está de baja.</summary>
public sealed record ZonaHistorial(int Id, string Nombre, string Tipo, short? Aforo, bool ConNodo);

/// <summary>OK: hay dato. SIN_DATOS: la zona no tuvo un nodo En línea la mayor parte de esa hora. FUTURO: aún no llega.</summary>
public static class EstadoCelda
{
    public const string Ok = "OK", SinDatos = "SIN_DATOS", Futuro = "FUTURO";
}

/// <summary>
/// Personas en promedio en esa hora (con un decimal). Nulo si no es OK. FueraDeServicio: la zona estuvo
/// fuera de servicio la mayor parte de la hora (ADR-25); el valor no se altera, solo se marca.
/// </summary>
public sealed record CeldaMapa(int Hora, double? Personas, string Estado, bool FueraDeServicio = false);

/// <summary>Una fila del mapa: una fecha, o un día de la semana (1 = lunes … 7 = domingo) si el periodo es largo.</summary>
public sealed record FilaMapa(DateOnly? Fecha, int? DiaSemana, IReadOnlyList<CeldaMapa> Celdas);

/// <summary>
/// El mapa día × hora de una zona. Agrupacion: FECHA o DIA_SEMANA. Las horas van de HoraDesde a HoraHasta
/// (las del horario en que hubo atenciones en el periodo); nulas y sin filas si no hubo ninguna.
/// </summary>
public sealed record OcupacionHistorial(
    IReadOnlyList<ZonaHistorial> Zonas, int? ZonaId, string Agrupacion, int? HoraDesde, int? HoraHasta, IReadOnlyList<FilaMapa> Filas);

/// <summary>Una página de la tabla de atenciones.</summary>
public sealed record PaginaHistorial(IReadOnlyList<FilaHistorial> Filas, int Total, int Pagina, int Tamano);

/// <summary>Un tramo de tiempo [Desde, Hasta).</summary>
public readonly record struct Tramo(DateTime Desde, DateTime Hasta)
{
    public TimeSpan Solape(DateTime desde, DateTime hasta)
    {
        var a = Desde > desde ? Desde : desde;
        var b = Hasta < hasta ? Hasta : hasta;
        return b > a ? b - a : TimeSpan.Zero;
    }
}

/// <summary>Los cálculos del historial (P5). Son puros: se prueban sin base ni reloj del sistema.</summary>
public static class CalculoHistorial
{
    public const string PorFecha = "FECHA", PorDiaSemana = "DIA_SEMANA";

    /// <summary>Una hora cuenta si la zona tuvo un nodo En línea al menos la mitad de ella; si no, «sin datos».</summary>
    public const double CoberturaMinima = 0.5;

    public static FilaHistorial Fila(AtencionDetalleInfo a, Func<int, bool> esServicio, DateTime ahora)
    {
        var t = CalculoDetalle.Tiempos(a, esServicio, ahora);
        return new FilaHistorial(a.Id, a.Codigo, a.Fecha, a.InicioEn, a.FinEn, t.Estado, a.Motivo,
            t.EsperaMin, t.ConsultaMin, t.TotalMin);
    }

    public static ResumenHistorial Resumen(IReadOnlyList<FilaHistorial> filas)
    {
        var terminadas = filas.Where(f => f.Terminada).ToList();
        var esperas = terminadas.Where(f => f.ConsultaMin is not null).Select(f => f.EsperaMin).ToList();
        var totales = terminadas.Select(f => f.TotalMin).ToList();
        return new ResumenHistorial(filas.Count, terminadas.Count,
            esperas.Count, Mediana(esperas), Percentil90(esperas),
            Mediana(totales), Percentil90(totales));
    }

    /// <summary>Mediana en minutos completos (si son dos centrales, su promedio hacia abajo). Nula si no hay datos.</summary>
    public static int? Mediana(IReadOnlyCollection<int> valores)
    {
        if (valores.Count == 0) return null;
        var v = valores.Order().ToArray();
        var m = v.Length / 2;
        return v.Length % 2 == 1 ? v[m] : (v[m - 1] + v[m]) / 2;
    }

    /// <summary>
    /// Percentil 90 por rango más cercano: el valor tal que 9 de cada 10 están en él o por debajo. Es un valor
    /// que de verdad ocurrió, no una interpolación, para poder decir «9 de cada 10 esperaron X min o menos».
    /// </summary>
    public static int? Percentil90(IReadOnlyCollection<int> valores)
    {
        if (valores.Count == 0) return null;
        var v = valores.Order().ToArray();
        return v[(int)Math.Ceiling(0.9 * v.Length) - 1];
    }

    /// <summary>
    /// Las horas de Lima del horario observado: de la primera entrega a la última salida (o «ahora») de las
    /// atenciones del periodo. Así el mapa no muestra 24 columnas de madrugada vacías. Null si no hubo atenciones.
    /// </summary>
    public static (int Desde, int Hasta)? Horario(IReadOnlyList<AtencionDetalleInfo> atenciones, DateTime ahora, ZonaHoraria zona)
    {
        if (atenciones.Count == 0) return null;
        var desde = atenciones.Min(a => zona.ALocal(a.InicioEn).Hour);
        var hasta = atenciones.Max(a =>
        {
            var fin = zona.ALocal(a.FinEn ?? ahora);
            var inicio = zona.ALocal(a.InicioEn);
            // Si terminó a otro día (no debería pasar en consulta externa), se corta al fin de ese día
            return fin.Date > inicio.Date ? 23 : fin.Hour;
        });
        return (desde, Math.Max(desde, hasta));
    }

    /// <summary>
    /// Los tramos en que la zona tuvo al menos un nodo En línea. Cada nodo está En línea desde un cambio a
    /// En línea hasta su siguiente cambio (o hasta «hasta»); la zona tiene datos si alguno lo está.
    /// </summary>
    public static IReadOnlyList<Tramo> Cobertura(IReadOnlyList<CambioNodo> cambios, DateTime hasta)
    {
        var tramos = new List<Tramo>();
        foreach (var nodo in cambios.GroupBy(c => c.NodoId))
        {
            var lista = nodo.OrderBy(c => c.En).ToList();
            for (var i = 0; i < lista.Count; i++)
            {
                if (!lista[i].EnLinea) continue;
                var fin = i + 1 < lista.Count ? lista[i + 1].En : hasta;
                if (fin > lista[i].En) tramos.Add(new Tramo(lista[i].En, fin));
            }
        }
        return Unir(tramos);
    }

    /// <summary>Une los tramos que se tocan o se pisan, para no contar dos veces la hora que cubren dos nodos.</summary>
    public static IReadOnlyList<Tramo> Unir(IEnumerable<Tramo> tramos)
    {
        var unidos = new List<Tramo>();
        foreach (var t in tramos.OrderBy(t => t.Desde))
        {
            if (unidos.Count > 0 && t.Desde <= unidos[^1].Hasta)
                unidos[^1] = new Tramo(unidos[^1].Desde, t.Hasta > unidos[^1].Hasta ? t.Hasta : unidos[^1].Hasta);
            else
                unidos.Add(t);
        }
        return unidos;
    }

    /// <summary>
    /// El mapa día × hora de una zona. Cada celda = minutos-paciente dentro de la hora ÷ minutos de la hora:
    /// tres pacientes que estuvieron 20 min cada uno son «1 persona en promedio», no 3. La hora en curso se
    /// divide entre los minutos que ya pasaron. Con más de 7 días, cada fila es un día de la semana y cada
    /// celda el promedio de las fechas de ese día que tienen dato.
    /// </summary>
    public static (string Agrupacion, IReadOnlyList<FilaMapa> Filas) Mapa(
        Periodo periodo, int horaDesde, int horaHasta, IReadOnlyList<Tramo> estadias, IReadOnlyList<Tramo> cobertura,
        DateTime ahora, ZonaHoraria zona, IReadOnlyList<Tramo>? fueraDeServicio = null)
    {
        var pausas = Unir(fueraDeServicio ?? []);
        var porFecha = periodo.Fechas.Select(fecha => new FilaMapa(fecha, null,
            [.. Enumerable.Range(horaDesde, horaHasta - horaDesde + 1).Select(h => Celda(fecha, h, estadias, cobertura, pausas, ahora, zona))])).ToList();
        if (periodo.Dias <= Periodo.MaximoDiasPorFecha) return (PorFecha, porFecha);

        var porDia = porFecha
            .GroupBy(f => DiaSemana(f.Fecha!.Value))
            .OrderBy(g => g.Key)
            .Select(g => new FilaMapa(null, g.Key, [.. Enumerable.Range(0, horaHasta - horaDesde + 1).Select(i =>
            {
                var celdas = g.Select(f => f.Celdas[i]).ToList();
                var conDato = celdas.Where(c => c.Estado == EstadoCelda.Ok).ToList();
                var estado = conDato.Count > 0 ? EstadoCelda.Ok
                    : celdas.Any(c => c.Estado == EstadoCelda.SinDatos) ? EstadoCelda.SinDatos
                    : EstadoCelda.Futuro;
                return new CeldaMapa(horaDesde + i, conDato.Count > 0 ? Redondear(conDato.Average(c => c.Personas!.Value)) : null, estado,
                    celdas.Any(c => c.FueraDeServicio));
            })]))
            .ToList();
        return (PorDiaSemana, porDia);
    }

    /// <summary>1 = lunes … 7 = domingo (ISO), como se lee una semana en Perú.</summary>
    public static int DiaSemana(DateOnly fecha) => fecha.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)fecha.DayOfWeek;

    private static CeldaMapa Celda(DateOnly fecha, int hora, IReadOnlyList<Tramo> estadias, IReadOnlyList<Tramo> cobertura,
        IReadOnlyList<Tramo> pausas, DateTime ahora, ZonaHoraria zona)
    {
        var inicio = zona.AUtc(fecha, hora);
        if (inicio >= ahora) return new CeldaMapa(hora, null, EstadoCelda.Futuro);
        var finHora = zona.AUtc(fecha, hora) + TimeSpan.FromHours(1);
        var fin = finHora < ahora ? finHora : ahora;
        var duracion = fin - inicio;

        var fuera = pausas.Aggregate(TimeSpan.Zero, (s, t) => s + t.Solape(inicio, fin)).TotalSeconds >= CoberturaMinima * duracion.TotalSeconds;
        var cubierto = cobertura.Aggregate(TimeSpan.Zero, (s, t) => s + t.Solape(inicio, fin));
        if (cubierto.TotalSeconds < CoberturaMinima * duracion.TotalSeconds)
            return new CeldaMapa(hora, null, EstadoCelda.SinDatos, fuera);

        var presencia = estadias.Aggregate(TimeSpan.Zero, (s, t) => s + t.Solape(inicio, fin));
        return new CeldaMapa(hora, Redondear(presencia / duracion), EstadoCelda.Ok, fuera);
    }

    private static double Redondear(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);
}
