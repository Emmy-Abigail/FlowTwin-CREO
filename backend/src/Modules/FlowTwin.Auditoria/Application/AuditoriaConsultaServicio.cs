using System.Text.Json;
using FlowTwin.Auditoria.Contracts;
using FlowTwin.Auditoria.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Auditoria.Application;

public sealed record FiltroAuditoria(DateOnly Desde, DateOnly Hasta, int? UsuarioId, string? Categoria, string? Buscar);

/// <summary>Objeto: el nombre o código de lo que se tocó («Módulo 2», «Luis Ramos»), tomado de lo que quedó guardado.</summary>
public sealed record FilaAuditoria(long Id, DateTime Cuando, int UsuarioId, string Usuario, string Accion, string AccionTexto,
    string Entidad, string? EntidadId, string? Objeto, string Categoria);

public sealed record PaginaAuditoria(IReadOnlyList<FilaAuditoria> Filas, int Total, int Pagina, int Tamano, IReadOnlyDictionary<string, int> Conteos);

public sealed record DetalleAuditoria(FilaAuditoria Fila, string? Ip, string? TraceId, JsonElement? Antes, JsonElement? Despues);

/// <summary>
/// P11 · Auditoría: consulta de la bitácora (solo Administrador). Siempre acotada por un período de días de Lima,
/// paginada en el servidor, y de solo lectura: la bitácora no se edita desde aquí ni desde ninguna parte.
/// </summary>
public sealed class AuditoriaConsultaServicio(
    IAuditoriaDb db, INombresDeUsuarios nombres, ZonaHoraria zona, IAuditoria auditoria)
{
    public const int TamanoPagina = 25;
    public const int MaximoDias = 366;
    public const int MaximoExportacion = 10_000;
    private const int LargoBusquedaMaximo = 60;

    public async Task<PaginaAuditoria> ListarAsync(FiltroAuditoria f, int? pagina, CancellationToken ct)
    {
        var baseConsulta = Filtrar(f);

        // Conteos de cada categoría con el resto de filtros aplicados (una sola consulta agrupada por acción y entidad)
        var grupos = await baseConsulta.GroupBy(a => new { a.AccionAuditoriaId, a.Entidad })
            .Select(g => new { g.Key.AccionAuditoriaId, g.Key.Entidad, N = g.Count() }).ToListAsync(ct);
        var conteos = CategoriasAuditoria.Todas.ToDictionary(c => c, _ => 0);
        foreach (var g in grupos) conteos[CategoriasAuditoria.De(g.AccionAuditoriaId, g.Entidad)] += g.N;

        var filtrada = f.Categoria is null ? baseConsulta : baseConsulta.Where(CategoriasAuditoria.Predicado(f.Categoria));
        var total = f.Categoria is null ? conteos.Values.Sum() : conteos[f.Categoria];
        var numero = Math.Max(1, pagina ?? 1);
        var filas = await filtrada.OrderByDescending(a => a.FechaRegistro).ThenByDescending(a => a.Id)
            .Skip((numero - 1) * TamanoPagina).Take(TamanoPagina).ToListAsync(ct);

        var porUsuario = await NombresAsync(filas, ct);
        return new PaginaAuditoria([.. filas.Select(a => Fila(a, porUsuario))], total, numero, TamanoPagina, conteos);
    }

    public async Task<DetalleAuditoria> DetalleAsync(long id, CancellationToken ct)
    {
        var a = await db.Auditoria.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NoEncontradoException("No existe ese registro de auditoría.");
        var porUsuario = await NombresAsync([a], ct);
        return new DetalleAuditoria(Fila(a, porUsuario), a.Ip, a.TraceId, Json(a.ValorAntes), Json(a.ValorDespues));
    }

    /// <summary>
    /// CSV del filtro, con tope: si hay más filas se pide reducir el período o filtrar, en vez de entregar un archivo
    /// incompleto sin avisar. La exportación queda en la propia bitácora.
    /// </summary>
    public async Task<(byte[] Contenido, string Nombre)> CsvAsync(FiltroAuditoria f, CancellationToken ct)
    {
        var consulta = Filtrar(f);
        if (f.Categoria is not null) consulta = consulta.Where(CategoriasAuditoria.Predicado(f.Categoria));
        var total = await consulta.CountAsync(ct);
        if (total > MaximoExportacion)
            throw new ReglaNegocioException("EXPORTACION_MUY_GRANDE",
                $"Hay {total:N0} registros y el máximo por archivo es {MaximoExportacion:N0}: reduce el período o filtra por categoría.");

        var filas = await consulta.OrderByDescending(a => a.FechaRegistro).ThenByDescending(a => a.Id).ToListAsync(ct);
        var porUsuario = await NombresAsync(filas, ct);
        var exportadas = filas.Select(a =>
        {
            var fila = Fila(a, porUsuario);
            return new FilaExportada(a.FechaRegistro, fila.Usuario, fila.AccionTexto, fila.Categoria, fila.Entidad, fila.Objeto, a.Ip);
        });
        var contenido = CsvAuditoria.Generar(exportadas, zona);

        auditoria.Registrar(AccionAuditoria.Exportar, "Reporte", "auditoria",
            despues: new { formato = "CSV", desde = f.Desde, hasta = f.Hasta, usuarioId = f.UsuarioId, categoria = f.Categoria, buscar = Limpiar(f.Buscar), filas = total });
        await db.GuardarAsync(ct);
        return (contenido, $"auditoria_{f.Desde:yyyy-MM-dd}_{f.Hasta:yyyy-MM-dd}.csv");
    }

    /// <summary>Valida el período y los filtros; la misma regla para la lista, el CSV y la verificación.</summary>
    public static FiltroAuditoria Validar(DateOnly desde, DateOnly hasta, int? usuarioId, string? categoria, string? buscar)
    {
        if (hasta < desde) throw new ReglaNegocioException("PERIODO_INVALIDO", "La fecha final no puede ser anterior a la inicial.");
        if (hasta.DayNumber - desde.DayNumber + 1 > MaximoDias)
            throw new ReglaNegocioException("PERIODO_MUY_LARGO", $"El período máximo es de {MaximoDias} días.");
        if (!string.IsNullOrEmpty(categoria) && !CategoriasAuditoria.EsValida(categoria))
            throw new ReglaNegocioException("CATEGORIA_INVALIDA", "La categoría no existe.");
        return new FiltroAuditoria(desde, hasta, usuarioId, string.IsNullOrEmpty(categoria) ? null : categoria, Limpiar(buscar));
    }

    private IQueryable<RegistroAuditoria> Filtrar(FiltroAuditoria f)
    {
        var inicio = zona.AUtc(f.Desde, 0);
        var fin = zona.AUtc(f.Hasta.AddDays(1), 0);
        var consulta = db.Auditoria.AsNoTracking().Where(a => a.FechaRegistro >= inicio && a.FechaRegistro < fin);
        if (f.UsuarioId is { } usuario) consulta = consulta.Where(a => a.UsuarioRegistroId == usuario);
        if (f.Buscar is { Length: > 0 } texto)
        {
            var patron = $"%{EscaparLike(texto)}%";
            // El código técnico (TraceId) identifica UNA petición: se busca exacto, para que un técnico encuentre
            // ese registro a partir de lo que vio en los registros del servidor
            consulta = consulta.Where(a => a.TraceId == texto || EF.Functions.Like(a.EntidadId!, patron, "\\")
                || EF.Functions.Like(a.ValorDespues!, patron, "\\") || EF.Functions.Like(a.ValorAntes!, patron, "\\"));
        }
        return consulta;
    }

    /// <summary>Quita lo que no es texto de una persona y limita el largo; vacío = sin búsqueda.</summary>
    private static string? Limpiar(string? texto)
    {
        var t = new string((texto ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (t.Length > LargoBusquedaMaximo) t = t[..LargoBusquedaMaximo];
        return t.Length == 0 ? null : t;
    }

    private static string EscaparLike(string t) => t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    /// <summary>Los nombres de quien actuó y, en las filas sobre un usuario, también del usuario afectado.</summary>
    private Task<IReadOnlyDictionary<int, string>> NombresAsync(IReadOnlyCollection<RegistroAuditoria> filas, CancellationToken ct) =>
        nombres.NombresAsync([.. filas.Select(a => a.UsuarioRegistroId).Concat(filas.Select(UsuarioAfectado).OfType<int>()).Distinct()], ct);

    private static int? UsuarioAfectado(RegistroAuditoria a) =>
        a.Entidad == "Usuario" && int.TryParse(a.EntidadId, out var id) ? id : null;

    private static FilaAuditoria Fila(RegistroAuditoria a, IReadOnlyDictionary<int, string> porUsuario)
    {
        // Un inicio de sesión o una baja no guardan datos: el objeto sería solo «2»; se dice el nombre de esa persona
        var objeto = ObjetoDe(a);
        if (objeto == a.EntidadId && UsuarioAfectado(a) is { } afectado && porUsuario.TryGetValue(afectado, out var nombre)) objeto = nombre;
        return new(a.Id, a.FechaRegistro, a.UsuarioRegistroId, porUsuario.GetValueOrDefault(a.UsuarioRegistroId, $"Usuario {a.UsuarioRegistroId}"),
            a.AccionAuditoriaId.Codigo(), a.AccionAuditoriaId.Nombre(), a.Entidad, a.EntidadId, objeto, CategoriasAuditoria.De(a.AccionAuditoriaId, a.Entidad));
    }

    /// <summary>
    /// El nombre de lo que se tocó, de lo que quedó guardado en el «después» (o el «antes» si se dio de baja):
    /// nombres + apellidos de un usuario, o el primero de nombre, código o correo; si no hay, el id.
    /// </summary>
    public static string? ObjetoDe(RegistroAuditoria a) => ObjetoDe(a.EntidadId, a.ValorDespues, a.ValorAntes);

    public static string? ObjetoDe(string? entidadId, params string?[] valores)
    {
        foreach (var texto in valores)
        {
            if (Json(texto) is not { ValueKind: JsonValueKind.Object } o) continue;
            string? Texto(string clave) => o.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, clave, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.String } p ? p.Value.GetString() : null;
            var persona = $"{Texto("nombres")} {Texto("apellidos")}".Trim();
            var nombre = persona.Length > 0 ? persona : Texto("nombre") ?? Texto("codigo") ?? Texto("correo");
            if (!string.IsNullOrWhiteSpace(nombre)) return nombre;
        }
        return entidadId;
    }

    private static JsonElement? Json(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try { return JsonDocument.Parse(texto).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }
}
