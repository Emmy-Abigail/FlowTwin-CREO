using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Dispositivos.Application;

/// <summary>Una página del inventario con los conteos de cada filtro (para los chips de la pantalla).</summary>
public sealed record PaginaInventario<T>(IReadOnlyList<T> Filas, int Total, int Pagina, int Tamano, IReadOnlyDictionary<string, int> Conteos);

/// <summary>Problema: motivo si requiere atención (EXTRAVIADA, NUNCA_DETECTADA, SIN_DETECCION), o null.</summary>
public sealed record TarjetaInventarioDto(int Id, string Codigo, string Estado, string EstadoNombre, string CodigoQr,
    string? Problema, DateTime? UltimaDeteccionEn, string? ZonaUltimaDeteccion);

/// <summary>PosX, PosY y AjusteRssiDb van para que «Cambiar zona» los conserve (la edición los pide completos).</summary>
public sealed record NodoInventarioDto(int Id, string Codigo, string Mac, int ZonaId, string Zona, int PisoId, string Piso,
    string Estado, DateTime? UltimoLatidoEn, bool RequiereAtencion, decimal? PosX, decimal? PosY, short AjusteRssiDb);

public sealed record RegistrarTarjetasRequest(int SedeId, int Desde, int Hasta);

public sealed record TarjetaRegistradaDto(string Codigo, string CodigoQr);

/// <summary>
/// P9 · Dispositivos (solo Administrador): inventario de tarjetas y nodos «por excepción». Abre en lo que
/// requiere atención; lo demás se encuentra con filtros y búsqueda, paginado en el servidor (escala a miles).
/// </summary>
public sealed class InventarioServicio(IDispositivosDb db, IZonasConsultas zonas, ISedesConsultas sedes, IAuditoria auditoria, IReloj reloj)
    : ITarjetasDeteccion
{
    public const int Tamano = 8;
    /// <summary>Tope de un registro por rango: un error de tipeo (1 a 50000) no crea miles de tarjetas.</summary>
    public const int MaximoPorLote = 500;
    public const string FiltroAtencion = "ATENCION", FiltroTodas = "TODAS", FiltroEnLinea = "EN_LINEA";

    // ---------- Tarjetas

    public async Task<PaginaInventario<TarjetaInventarioDto>> TarjetasAsync(int sedeId, string? filtro, string? buscar, int? pagina, CancellationToken ct)
    {
        var ahora = reloj.AhoraUtc;
        var deSede = db.Tarjetas.AsNoTracking().Where(t => t.SedeId == sedeId);
        var porEstado = await deSede.GroupBy(t => t.EstadoTarjetaId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var conteos = porEstado.ToDictionary(x => x.Key.Codigo(), x => x.N);
        conteos[FiltroAtencion] = await deSede.CountAsync(ReglasInventario.TarjetaRequiereAtencion(ahora), ct);
        conteos[FiltroTodas] = porEstado.Sum(x => x.N);

        var consulta = (filtro ?? FiltroAtencion).ToUpperInvariant() switch
        {
            FiltroAtencion => deSede.Where(ReglasInventario.TarjetaRequiereAtencion(ahora)),
            FiltroTodas => deSede,
            var codigo => CatalogoExtensiones.DesdeCodigo<EstadoTarjeta>(codigo) is { } estado
                ? deSede.Where(t => t.EstadoTarjetaId == estado)
                : throw new ReglaNegocioException("FILTRO_INVALIDO", "Ese filtro no existe."),
        };
        if (Codigo(buscar) is { } q) consulta = consulta.Where(t => t.Codigo.Contains(q));

        var total = await consulta.CountAsync(ct);
        var p = Pagina(pagina, total);
        var filas = await consulta.OrderBy(t => t.Codigo.Length).ThenBy(t => t.Codigo)
            .Skip((p - 1) * Tamano).Take(Tamano).ToListAsync(ct);

        var zonaDeNodo = await ZonasDeNodosAsync([.. filas.Where(t => t.UltimoNodoId is not null).Select(t => t.UltimoNodoId!.Value)], ct);
        return new PaginaInventario<TarjetaInventarioDto>([.. filas.Select(t => new TarjetaInventarioDto(
            t.Id, t.Codigo, t.EstadoTarjetaId.Codigo(), t.EstadoTarjetaId.Nombre(), t.CodigoQr,
            ReglasInventario.MotivoTarjeta(t.EstadoTarjetaId, t.UltimaDeteccionEn, t.FechaRegistro, ahora),
            t.UltimaDeteccionEn, t.UltimoNodoId is { } n ? zonaDeNodo.GetValueOrDefault(n) : null))],
            total, p, Tamano, conteos);
    }

    /// <summary>Registra las tarjetas T-desde … T-hasta de la sede, todas o ninguna. Devuelve sus códigos y textos de QR.</summary>
    public async Task<IReadOnlyList<TarjetaRegistradaDto>> RegistrarAsync(RegistrarTarjetasRequest req, CancellationToken ct)
    {
        if (await sedes.NombreSedeAsync(req.SedeId, ct) is null) throw new NoEncontradoException("La sede no existe.");
        if (req.Hasta < req.Desde)
            throw new ReglaNegocioException("RANGO_INVALIDO", "El número final no puede ser menor que el inicial.");
        if (req.Hasta - req.Desde + 1 > MaximoPorLote)
            throw new ReglaNegocioException("RANGO_INVALIDO", $"Como máximo {MaximoPorLote} tarjetas por registro.");

        var nuevas = Enumerable.Range(req.Desde, req.Hasta - req.Desde + 1).Select(n => Tarjeta.Registrar(n, req.SedeId)).ToList();
        var codigos = nuevas.Select(t => t.Codigo).ToList();
        var existentes = await db.Tarjetas.IgnoreQueryFilters().Where(t => codigos.Contains(t.Codigo)).Select(t => t.Codigo).ToListAsync(ct);
        if (existentes.Count > 0)
            throw new ReglaNegocioException("TARJETAS_EXISTENTES", $"Ya existen: {string.Join(", ", existentes.Take(5))}{(existentes.Count > 5 ? "…" : "")}.",
                new { existentes });

        return await db.EnTransaccionAsync<IReadOnlyList<TarjetaRegistradaDto>>(async ct =>
        {
            db.Tarjetas.AddRange(nuevas);
            await db.GuardarAsync(ct);
            db.MovimientosTarjeta.AddRange(nuevas.Select(t => new MovimientoTarjeta
            {
                TarjetaId = t.Id, EstadoNuevoId = EstadoTarjeta.Disponible, Observacion = "Registrada",
            }));
            auditoria.Registrar(AccionAuditoria.Crear, "Tarjeta", $"{codigos[0]}…{codigos[^1]}", despues: new { sedeId = req.SedeId, cantidad = nuevas.Count });
            await db.GuardarAsync(ct);
            return [.. nuevas.Select(t => new TarjetaRegistradaDto(t.Codigo, t.CodigoQr))];
        }, ct);
    }

    /// <summary>Retira una tarjeta dañada o perdida. La máquina de estados decide desde dónde se puede (no En uso ni Sin devolver).</summary>
    public async Task DarDeBajaTarjetaAsync(int id, CancellationToken ct)
    {
        var t = await db.Tarjetas.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException("La tarjeta no existe.");
        var antes = t.EstadoTarjetaId;
        db.MovimientosTarjeta.Add(t.CambiarA(EstadoTarjeta.Baja, observacion: "Dada de baja por el Administrador"));
        auditoria.Registrar(AccionAuditoria.DarBaja, "Tarjeta", t.Codigo, new { estado = antes.Codigo() }, new { estado = EstadoTarjeta.Baja.Codigo() });
        await db.GuardarAsync(ct);
    }

    public async Task<int> RegistrarAsync(IReadOnlyList<DeteccionTarjeta> detecciones, CancellationToken ct = default)
    {
        if (detecciones.Count == 0) return 0;
        var ultimas = detecciones.GroupBy(d => d.UuidBle.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.MaxBy(d => d.En)!);
        var uuids = ultimas.Keys.ToList();
        // Los identificadores se guardan en minúsculas (EmisorBle): se compara directo para usar el índice único
        var tarjetas = await db.Tarjetas.Where(t => uuids.Contains(t.UuidBle)).ToListAsync(ct);
        var cambiadas = 0;
        foreach (var t in tarjetas)
        {
            var d = ultimas[t.UuidBle];
            var antes = t.UltimaDeteccionEn;
            t.RegistrarDeteccion(d.En, d.NodoId);
            if (t.UltimaDeteccionEn != antes) cambiadas++;
        }
        if (cambiadas > 0) await db.GuardarAsync(ct);
        return cambiadas;
    }

    // ---------- Nodos

    public async Task<PaginaInventario<NodoInventarioDto>> NodosAsync(string? filtro, string? buscar, int? pagina, CancellationToken ct)
    {
        var activos = db.Nodos.AsNoTracking().Where(n => n.EstadoNodoId != EstadoNodo.Baja);
        var conteos = new Dictionary<string, int>
        {
            [FiltroAtencion] = await activos.CountAsync(ReglasInventario.NodoRequiereAtencion(), ct),
            [FiltroEnLinea] = await activos.CountAsync(n => n.EstadoNodoId == EstadoNodo.EnLinea, ct),
            [FiltroTodas] = await activos.CountAsync(ct),
        };
        var consulta = (filtro ?? FiltroAtencion).ToUpperInvariant() switch
        {
            FiltroAtencion => activos.Where(ReglasInventario.NodoRequiereAtencion()),
            FiltroEnLinea => activos.Where(n => n.EstadoNodoId == EstadoNodo.EnLinea),
            FiltroTodas => activos,
            _ => throw new ReglaNegocioException("FILTRO_INVALIDO", "Ese filtro no existe."),
        };
        if (buscar?.Trim() is { Length: > 0 } texto)
        {
            var codigo = Nodo.NormalizarCodigo(texto);
            consulta = consulta.Where(n => n.Codigo.Contains(codigo) || n.Codigo.Contains(texto.ToUpper()));
        }
        var total = await consulta.CountAsync(ct);
        var p = Pagina(pagina, total);
        var filas = await consulta.OrderBy(n => n.Codigo.Length).ThenBy(n => n.Codigo).Skip((p - 1) * Tamano).Take(Tamano).ToListAsync(ct);

        var zonasPorId = (await zonas.ZonasAsync([.. filas.Select(n => n.ZonaId).Distinct()], ct)).ToDictionary(z => z.Id);
        var pisos = new Dictionary<int, string>();
        foreach (var pisoId in zonasPorId.Values.Select(z => z.PisoId).Distinct())
            pisos[pisoId] = (await zonas.PisoAsync(pisoId, ct))?.Nombre ?? $"Piso {pisoId}";

        return new PaginaInventario<NodoInventarioDto>([.. filas.Select(n =>
        {
            var z = zonasPorId.GetValueOrDefault(n.ZonaId);
            return new NodoInventarioDto(n.Id, n.Codigo, n.Mac, n.ZonaId, z?.Nombre ?? $"Zona {n.ZonaId}", z?.PisoId ?? 0,
                z is null ? "—" : pisos[z.PisoId], n.EstadoNodoId.Codigo(), n.UltimoLatidoEn, n.EstadoNodoId == EstadoNodo.SinSenal,
                n.PosX, n.PosY, n.AjusteRssiDb);
        })], total, p, Tamano, conteos);
    }

    // ---------- Apoyo

    private async Task<Dictionary<int, string>> ZonasDeNodosAsync(IReadOnlyCollection<int> nodoIds, CancellationToken ct)
    {
        if (nodoIds.Count == 0) return [];
        var nodos = await db.Nodos.AsNoTracking().IgnoreQueryFilters().Where(n => nodoIds.Contains(n.Id)).Select(n => new { n.Id, n.ZonaId }).ToListAsync(ct);
        var nombres = (await zonas.ParaRecorridoAsync([.. nodos.Select(n => n.ZonaId).Distinct()], ct)).ToDictionary(z => z.Id, z => z.Nombre);
        return nodos.ToDictionary(n => n.Id, n => nombres.GetValueOrDefault(n.ZonaId, $"Zona {n.ZonaId}"));
    }

    /// <summary>«12», «t12» o «T-0012» → «T-12» para buscar; un texto que no es un código se busca tal cual.</summary>
    private static string? Codigo(string? buscar) =>
        string.IsNullOrWhiteSpace(buscar) ? null : Tarjeta.NormalizarCodigo(buscar.Length > 12 ? buscar[..12] : buscar);

    private static int Pagina(int? pagina, int total) => Math.Clamp(pagina ?? 1, 1, Math.Max(1, (int)Math.Ceiling(total / (double)Tamano)));
}
