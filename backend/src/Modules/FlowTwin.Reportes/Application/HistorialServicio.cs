using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.Reportes.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.Reportes.Application;

/// <summary>
/// Historial y reportes (P5) de un piso: pide cada dato a su módulo por contrato y lo calcula con
/// CalculoHistorial. Las atenciones son de la sede del piso; la ocupación, de una zona del piso.
/// </summary>
public sealed class HistorialServicio(
    IZonasConsultas zonas, IAtencionesConsulta atenciones, IHistorialNodos historialNodos, IPresenciaDispositivos dispositivos,
    IUsuarioActual usuario, IReloj reloj, ZonaHoraria zonaHoraria, IAuditoria auditoria, IUnidadDeTrabajo unidad)
{
    /// <summary>Filas por página de la tabla: las que entran sin scroll en 1280×720.</summary>
    public const int TamanoPorDefecto = 8, TamanoMaximo = 50;

    /// <summary>Tipos de zona sin mapa: por Tránsito y Salida solo se pasa (RN-13).</summary>
    private static readonly string[] SinMapa = ["TRANSITO", "SALIDA"];

    /// <summary>La zona que se abre primero: donde se acumula la gente.</summary>
    private static readonly string[] OrdenTipos = ["ESPERA", "SERVICIO", "INGRESO"];

    public async Task<ResumenHistorial> ResumenAsync(int pisoId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var c = await ContextoAsync(pisoId, desde, hasta, ct);
        return CalculoHistorial.Resumen(c.Filas);
    }

    public async Task<PaginaHistorial> AtencionesAsync(int pisoId, DateOnly desde, DateOnly hasta, string? codigo, int? pagina, int? tamano, CancellationToken ct)
    {
        var c = await ContextoAsync(pisoId, desde, hasta, ct);
        var filas = Buscar(c.Filas, codigo);
        var t = Math.Clamp(tamano ?? TamanoPorDefecto, 1, TamanoMaximo);
        var total = filas.Count;
        var paginas = Math.Max(1, (int)Math.Ceiling(total / (double)t));
        var p = Math.Clamp(pagina ?? 1, 1, paginas);
        return new PaginaHistorial([.. filas.Skip((p - 1) * t).Take(t)], total, p, t);
    }

    /// <summary>Todas las filas del periodo (con el filtro de código) en CSV. Queda en la auditoría: quién y con qué filtros.</summary>
    public async Task<(byte[] Contenido, string Nombre)> CsvAsync(int pisoId, DateOnly desde, DateOnly hasta, string? codigo, CancellationToken ct)
    {
        var c = await ContextoAsync(pisoId, desde, hasta, ct);
        var filas = Buscar(c.Filas, codigo);
        auditoria.Registrar(AccionAuditoria.Exportar, "Reporte", "historial-atenciones",
            despues: new { formato = "CSV", pisoId, desde, hasta, codigo = Limpiar(codigo), filas = filas.Count });
        await unidad.GuardarAsync(ct);
        return (CsvHistorial.Generar(filas, zonaHoraria), $"atenciones_{c.Piso.Nombre.Replace(' ', '-').ToLowerInvariant()}_{desde:yyyy-MM-dd}_{hasta:yyyy-MM-dd}.csv");
    }

    public async Task<OcupacionHistorial> OcupacionAsync(int pisoId, DateOnly desde, DateOnly hasta, int? zonaId, CancellationToken ct)
    {
        var c = await ContextoAsync(pisoId, desde, hasta, ct);
        var conNodo = (await dispositivos.NodosAsync(ct)).Select(n => n.ZonaId).ToHashSet();
        var lista = c.ZonasPiso
            .Where(z => !SinMapa.Contains(z.Tipo))
            .OrderBy(z => Prioridad(z.Tipo)).ThenBy(z => z.Id)
            .Select(z => new ZonaHistorial(z.Id, z.Nombre, z.Tipo, z.Aforo, conNodo.Contains(z.Id)))
            .ToList();
        var zona = lista.FirstOrDefault(z => z.Id == zonaId) ?? lista.FirstOrDefault();
        var horario = CalculoHistorial.Horario(c.Atenciones, c.Ahora, zonaHoraria);
        var agrupacion = c.Periodo.Dias <= Periodo.MaximoDiasPorFecha ? CalculoHistorial.PorFecha : CalculoHistorial.PorDiaSemana;
        if (zona is null || horario is not var (horaDesde, horaHasta))
            return new OcupacionHistorial(lista, zona?.Id, agrupacion, null, null, []);

        var inicio = zonaHoraria.AUtc(c.Periodo.Desde, 0);
        var fin = zonaHoraria.AUtc(c.Periodo.Hasta.AddDays(1), 0);
        var hastaCalculo = fin < c.Ahora ? fin : c.Ahora;
        var cobertura = CalculoHistorial.Cobertura(await historialNodos.DeZonasAsync([zona.Id], inicio, hastaCalculo, ct), hastaCalculo);
        var estadias = c.Atenciones
            .SelectMany(a => a.Estadias.Where(e => e.ZonaId == zona.Id).Select(e => new Tramo(e.EntradaEn, e.SalidaEn ?? a.FinEn ?? c.Ahora)))
            .ToList();
        var (grupo, filas) = CalculoHistorial.Mapa(c.Periodo, horaDesde, horaHasta, estadias, cobertura, c.Ahora, zonaHoraria);
        return new OcupacionHistorial(lista, zona.Id, grupo, horaDesde, horaHasta, filas);
    }

    private static int Prioridad(string tipo)
    {
        var i = Array.IndexOf(OrdenTipos, tipo);
        return i < 0 ? OrdenTipos.Length : i;
    }

    private sealed record Contexto(
        PisoInfo Piso, Periodo Periodo, DateTime Ahora, IReadOnlyList<ZonaSemaforo> ZonasPiso,
        IReadOnlyList<AtencionDetalleInfo> Atenciones, IReadOnlyList<FilaHistorial> Filas);

    /// <summary>RN-24 (403 si el piso no es del usuario), el piso (404) y el periodo (400); luego las atenciones calculadas.</summary>
    private async Task<Contexto> ContextoAsync(int pisoId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (!usuario.PuedeVerPiso(pisoId)) throw new SinPermisoException("No tienes asignado este piso.");
        var piso = await zonas.PisoAsync(pisoId, ct)
            ?? throw new NoEncontradoException($"No existe el piso {pisoId} o aún no está publicado.");
        var ahora = reloj.AhoraUtc;
        var periodo = Periodo.Validar(desde, hasta, zonaHoraria.FechaLocal(ahora));

        var delPeriodo = await atenciones.DelPeriodoAsync(piso.SedeId, periodo.Desde, periodo.Hasta, ct);
        // Los tipos de TODAS las zonas recorridas, como en P6: así la fila y su detalle dan los mismos minutos
        var tipos = (await zonas.ParaRecorridoAsync([.. delPeriodo.SelectMany(a => a.Estadias).Select(e => e.ZonaId).Distinct()], ct))
            .ToDictionary(z => z.Id, z => z.Tipo);
        bool esServicio(int id) => tipos.GetValueOrDefault(id) == ReglaTiempos.Servicio;
        var filas = delPeriodo
            .Select(a => CalculoHistorial.Fila(a, esServicio, ahora))
            .OrderByDescending(f => f.InicioEn)
            .ToList();
        return new Contexto(piso, periodo, ahora, await zonas.DelPisoAsync(pisoId, ct), delPeriodo, filas);
    }

    /// <summary>Búsqueda por código: «0042», «a-0042» o «A-0042». Los códigos se repiten cada día: la fecha los distingue.</summary>
    private static List<FilaHistorial> Buscar(IReadOnlyList<FilaHistorial> filas, string? codigo)
    {
        var q = Limpiar(codigo);
        return q is null ? [.. filas] : [.. filas.Where(f => f.Codigo.Contains(q, StringComparison.OrdinalIgnoreCase))];
    }

    private static string? Limpiar(string? codigo)
    {
        var q = codigo?.Trim();
        return string.IsNullOrEmpty(q) ? null : q.Length > 20 ? q[..20] : q;
    }
}
