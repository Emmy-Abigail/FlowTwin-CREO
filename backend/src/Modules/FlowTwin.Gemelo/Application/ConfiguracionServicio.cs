using FlowTwin.Gemelo.Contracts;
using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Gemelo.Application;

public sealed record PisoConfigDto(int Id, string Nombre, short Nivel, bool Publicado);
public sealed record SedeConfigDto(int Id, string Nombre, string? Direccion, char PrefijoAtencion, IReadOnlyList<PisoConfigDto> Pisos);
public sealed record ZonaConfigDto(int Id, string Nombre, string Tipo, short? Aforo, bool TieneAforo);

public sealed record SedeRequest(string Nombre, string? Direccion, char PrefijoAtencion);
public sealed record PisoRequest(string Nombre, short Nivel);
public sealed record ZonaRequest(string Nombre, string Tipo, short? Aforo);
public sealed record ParametrosRequest(short EsperaAltaMin, short EsperaCriticaMin, short SinLecturasMin, byte HoraCierre, byte SemaforoAmbarPct);

/// <summary>
/// Configuración (ADR-27, solo Administrador): sedes, pisos y zonas como datos, sin dibujo (la maqueta 3D la
/// instala el equipo técnico), y los parámetros del sistema. Nada se borra: las zonas se dan de baja (RN-28).
/// </summary>
public sealed class ConfiguracionServicio(IGemeloDb db, INodosDeZonas nodos, IAuditoria auditoria, CacheParametros cache, IReloj reloj)
{
    public async Task<IReadOnlyList<SedeConfigDto>> ArbolAsync(CancellationToken ct)
    {
        var sedes = await db.Sedes.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);
        var pisos = await db.Pisos.AsNoTracking().OrderBy(p => p.Nivel).ToListAsync(ct);
        return [.. sedes.Select(s => new SedeConfigDto(s.Id, s.Nombre, s.Direccion, s.PrefijoAtencion,
            [.. pisos.Where(p => p.SedeId == s.Id).Select(p => new PisoConfigDto(p.Id, p.Nombre, p.Nivel, p.Publicado))]))];
    }

    // ---------- Sedes

    public async Task<SedeConfigDto> CrearSedeAsync(SedeRequest req, CancellationToken ct)
    {
        var sede = new Sede();
        await AplicarAsync(sede, req, ct);
        db.Sedes.Add(sede);
        auditoria.Registrar(AccionAuditoria.Crear, "Sede", sede.Nombre, despues: req);
        await GuardarAsync(ct);
        return new SedeConfigDto(sede.Id, sede.Nombre, sede.Direccion, sede.PrefijoAtencion, []);
    }

    public async Task EditarSedeAsync(int id, SedeRequest req, CancellationToken ct)
    {
        var sede = await db.Sedes.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NoEncontradoException("La sede no existe.");
        var antes = new { sede.Nombre, sede.Direccion, sede.PrefijoAtencion };
        await AplicarAsync(sede, req, ct);
        auditoria.Registrar(AccionAuditoria.Editar, "Sede", sede.Nombre, antes, req);
        await GuardarAsync(ct);
    }

    private async Task AplicarAsync(Sede sede, SedeRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre?.Trim() ?? "";
        var prefijo = char.ToUpperInvariant(req.PrefijoAtencion);
        var problemas = new List<string>();
        if (nombre.Length is 0 or > 80) problemas.Add("El nombre de la sede va de 1 a 80 caracteres.");
        if (prefijo is < 'A' or > 'Z') problemas.Add("La letra de los códigos de atención va de la A a la Z.");
        if (req.Direccion?.Length > 200) problemas.Add("La dirección admite hasta 200 caracteres.");
        if (problemas.Count > 0) throw new ReglaNegocioException("SEDE_INVALIDA", "La sede no es válida.", new { problemas });
        // Una letra por sede: así un código A-0012 dice de qué sede es, sin ambigüedad
        if (await db.Sedes.AnyAsync(s => s.Id != sede.Id && s.PrefijoAtencion == prefijo, ct))
            throw new ReglaNegocioException("SEDE_PREFIJO_DUPLICADO", $"Otra sede ya usa la letra {prefijo}.");
        (sede.Nombre, sede.Direccion, sede.PrefijoAtencion) = (nombre, string.IsNullOrWhiteSpace(req.Direccion) ? null : req.Direccion.Trim(), prefijo);
    }

    // ---------- Pisos

    public async Task<PisoConfigDto> CrearPisoAsync(int sedeId, PisoRequest req, CancellationToken ct)
    {
        if (!await db.Sedes.AnyAsync(s => s.Id == sedeId, ct)) throw new NoEncontradoException("La sede no existe.");
        var piso = new Piso { SedeId = sedeId };   // nace en borrador: nadie lo ve en el gemelo hasta publicarlo (RN-15)
        Aplicar(piso, req);
        db.Pisos.Add(piso);
        auditoria.Registrar(AccionAuditoria.Crear, "Piso", piso.Nombre, despues: new { sedeId, req.Nombre, req.Nivel });
        await GuardarAsync(ct);
        return new PisoConfigDto(piso.Id, piso.Nombre, piso.Nivel, piso.Publicado);
    }

    public async Task EditarPisoAsync(int id, PisoRequest req, CancellationToken ct)
    {
        var piso = await PisoAsync(id, ct);
        var antes = new { piso.Nombre, piso.Nivel };
        Aplicar(piso, req);
        auditoria.Registrar(AccionAuditoria.Editar, "Piso", piso.Nombre, antes, req);
        await GuardarAsync(ct);
    }

    /// <summary>Publica el piso: aparece en el gemelo. Exige al menos una zona.</summary>
    public async Task PublicarPisoAsync(int id, CancellationToken ct)
    {
        var piso = await PisoAsync(id, ct);
        if (piso.Publicado) throw new ConflictoException($"{piso.Nombre} ya está publicado.");
        var zonas = await db.Zonas.Where(z => z.PisoId == id).ToListAsync(ct);
        if (zonas.Count == 0) throw new ReglaNegocioException("PISO_SIN_ZONAS", "Agrega al menos una zona antes de publicar el piso.");
        piso.Publicado = true;
        piso.PublicadoEn = reloj.AhoraUtc;
        foreach (var z in zonas) z.EnBorrador = false;
        auditoria.Registrar(AccionAuditoria.Publicar, "Piso", piso.Nombre);
        await db.GuardarAsync(ct);
    }

    private static void Aplicar(Piso piso, PisoRequest req)
    {
        var nombre = req.Nombre?.Trim() ?? "";
        if (nombre.Length is 0 or > 60) throw new ReglaNegocioException("PISO_INVALIDO", "El nombre del piso va de 1 a 60 caracteres.");
        if (req.Nivel is < -5 or > 60) throw new ReglaNegocioException("PISO_INVALIDO", "El nivel va de -5 (sótanos) a 60.");
        (piso.Nombre, piso.Nivel) = (nombre, req.Nivel);
    }

    private async Task<Piso> PisoAsync(int id, CancellationToken ct) =>
        await db.Pisos.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NoEncontradoException("El piso no existe.");

    // ---------- Zonas

    public async Task<IReadOnlyList<ZonaConfigDto>> ZonasAsync(int pisoId, CancellationToken ct)
    {
        await PisoAsync(pisoId, ct);
        var zonas = await db.Zonas.AsNoTracking().Where(z => z.PisoId == pisoId).OrderBy(z => z.Id).ToListAsync(ct);
        return [.. zonas.Select(Dto)];
    }

    public async Task<ZonaConfigDto> CrearZonaAsync(int pisoId, ZonaRequest req, CancellationToken ct)
    {
        var piso = await PisoAsync(pisoId, ct);
        var zona = Zona.Nueva(pisoId, req.Nombre ?? "", Tipo(req.Tipo), req.Aforo);
        zona.EnBorrador = !piso.Publicado;   // en un piso publicado, la zona nueva se ve enseguida
        zona.Validar();
        db.Zonas.Add(zona);
        auditoria.Registrar(AccionAuditoria.Crear, "Zona", zona.Nombre, despues: new { pisoId, req.Nombre, req.Tipo, req.Aforo });
        await GuardarAsync(ct);
        return Dto(zona);
    }

    /// <summary>Nombre, tipo y aforo. Cambiar a un tipo sin aforo (Tránsito o Salida) lo quita.</summary>
    public async Task<ZonaConfigDto> EditarZonaAsync(int id, ZonaRequest req, CancellationToken ct)
    {
        var zona = await db.Zonas.SingleOrDefaultAsync(z => z.Id == id, ct) ?? throw new NoEncontradoException("La zona no existe.");
        var antes = new { zona.Nombre, Tipo = zona.TipoZonaId.Codigo(), zona.Aforo };
        var tipo = Tipo(req.Tipo);
        (zona.Nombre, zona.TipoZonaId, zona.Aforo) = ((req.Nombre ?? "").Trim(), tipo, tipo.TieneAforo() ? req.Aforo : null);
        zona.Validar();
        auditoria.Registrar(AccionAuditoria.Editar, "Zona", zona.Nombre, antes, req);
        await GuardarAsync(ct);
        return Dto(zona);
    }

    /// <summary>Retira una zona que ya no existe en la clínica. No si tiene nodos: primero se mueven o se dan de baja (Dispositivos).</summary>
    public async Task DarDeBajaZonaAsync(int id, CancellationToken ct)
    {
        var zona = await db.Zonas.SingleOrDefaultAsync(z => z.Id == id, ct) ?? throw new NoEncontradoException("La zona no existe.");
        var conNodos = await nodos.ActivosAsync(id, ct);
        if (conNodos > 0)
            throw new ReglaNegocioException("ZONA_CON_NODOS", $"{zona.Nombre} tiene {conNodos} {(conNodos == 1 ? "nodo" : "nodos")}: muévelos de zona o dalos de baja en Dispositivos.");
        db.Zonas.Remove(zona);   // RN-28: el interceptor la marca como dada de baja; su historial se conserva
        auditoria.Registrar(AccionAuditoria.DarBaja, "Zona", zona.Nombre);
        await db.GuardarAsync(ct);
    }

    private static TipoZona Tipo(string? codigo) => CatalogoExtensiones.DesdeCodigo<TipoZona>(codigo)
        ?? throw new ReglaNegocioException("ZONA_TIPO_INVALIDO", "Elige un tipo de zona de la lista.");

    private static ZonaConfigDto Dto(Zona z) => new(z.Id, z.Nombre, z.TipoZonaId.Codigo(), z.Aforo, z.TipoZonaId.TieneAforo());

    // ---------- Parámetros

    public async Task<ParametrosInfo> ParametrosAsync(CancellationToken ct) =>
        ParametrosServicio.Info(await db.ParametrosSistema.AsNoTracking().SingleOrDefaultAsync(ct) ?? new ParametrosSistema());

    public async Task<ParametrosInfo> GuardarParametrosAsync(ParametrosRequest req, CancellationToken ct)
    {
        var p = await db.ParametrosSistema.SingleOrDefaultAsync(ct);
        if (p is null) { p = new ParametrosSistema(); db.ParametrosSistema.Add(p); }
        var antes = ParametrosServicio.Info(p);
        (p.EsperaAltaMin, p.EsperaCriticaMin, p.SinLecturasMin, p.HoraCierre, p.SemaforoAmbarPct) =
            (req.EsperaAltaMin, req.EsperaCriticaMin, req.SinLecturasMin, req.HoraCierre, req.SemaforoAmbarPct);
        p.Validar();
        auditoria.Registrar(AccionAuditoria.Editar, "Parametros", null, antes, req);
        await db.GuardarAsync(ct);
        cache.Olvidar();
        return ParametrosServicio.Info(p);
    }

    /// <summary>Los nombres repetidos los frena el índice único: se traducen a un mensaje claro.</summary>
    private async Task GuardarAsync(CancellationToken ct)
    {
        try { await db.GuardarAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
                                         || e.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ConflictoException("Ya existe otro con ese nombre o nivel. Usa uno distinto.");
        }
    }
}
