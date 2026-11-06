using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Dispositivos.Application;

public interface IDispositivosDb : IUnidadDeTrabajo
{
    DbSet<Tarjeta> Tarjetas { get; }
    DbSet<MovimientoTarjeta> MovimientosTarjeta { get; }
    DbSet<Nodo> Nodos { get; }
    DbSet<EventoNodo> EventosNodo { get; }
}

public sealed record TarjetaDto(int Id, string Codigo, string Estado, string EstadoNombre, DateTime? Desde);

/// <summary>Varias tarjetas a la vez: la tanda que el operador desinfectó o da por extraviada.</summary>
public sealed record TandaTarjetasRequest(int[] TarjetaIds);

public sealed class TandaTarjetasValidador : AbstractValidator<TandaTarjetasRequest>
{
    public const int Maximo = 100;

    public TandaTarjetasValidador()
    {
        RuleFor(x => x.TarjetaIds).NotEmpty().WithMessage("Marca al menos una tarjeta.")
            .Must(ids => ids.Length <= Maximo).WithMessage($"Como máximo {Maximo} tarjetas a la vez.")
            .Must(ids => ids.Distinct().Count() == ids.Length).WithMessage("Hay tarjetas repetidas.");
    }
}

/// <summary>Tarjetas BLE: consultas, desinfección (F-09) y extravío (F-10).</summary>
public sealed class TarjetasServicio(
    IDispositivosDb db, ISedesConsultas sedes, IAuditoria auditoria, IReloj reloj, IValidator<TandaTarjetasRequest> validadorTanda)
    : ITarjetasAdmision
{
    public async Task<TarjetaInfo?> BuscarAsync(string codigoOQr, CancellationToken ct = default)
    {
        var qr = codigoOQr.Trim();
        var codigo = Tarjeta.NormalizarCodigo(qr);
        var t = await db.Tarjetas.FirstOrDefaultAsync(x => x.CodigoQr == qr || x.Codigo == codigo, ct);
        return t is null ? null : await InfoAsync(t, ct);
    }

    public async Task<TarjetaInfo> CambiarEstadoAsync(int tarjetaId, EstadoTarjeta nuevo, long? atencionId = null,
        string? observacion = null, CancellationToken ct = default)
    {
        var tarjeta = await db.Tarjetas.SingleOrDefaultAsync(t => t.Id == tarjetaId, ct)
            ?? throw new NoEncontradoException("La tarjeta no existe.");
        db.MovimientosTarjeta.Add(tarjeta.CambiarA(nuevo, atencionId, observacion));
        return new TarjetaInfo(tarjeta.Id, tarjeta.Codigo, tarjeta.SedeId, tarjeta.EstadoTarjetaId, null);
    }

    public async Task<List<TarjetaInfo>> ListarAsync(IReadOnlyCollection<int>? sedeIds, CancellationToken ct = default)
    {
        var filas = await db.Tarjetas
            .Where(t => sedeIds == null || sedeIds.Contains(t.SedeId))
            .OrderBy(t => t.Codigo)
            .Select(t => new
            {
                t.Id, t.Codigo, t.SedeId, t.EstadoTarjetaId,
                Desde = db.MovimientosTarjeta.Where(m => m.TarjetaId == t.Id).Max(m => (DateTime?)m.FechaRegistro),
            })
            .ToListAsync(ct);
        return [.. filas.Select(f => new TarjetaInfo(f.Id, f.Codigo, f.SedeId, f.EstadoTarjetaId, f.Desde))];
    }

    public async Task<List<MovimientoInfo>> UltimasOperacionesAsync(IReadOnlyCollection<int>? sedeIds, int cantidad, CancellationToken ct = default)
    {
        return await db.MovimientosTarjeta
            .Join(db.Tarjetas, m => m.TarjetaId, t => t.Id, (m, t) => new { m, t })
            .Where(x => sedeIds == null || sedeIds.Contains(x.t.SedeId))
            .Where(x => x.m.EstadoAnteriorId != null)   // el alta (sin estado anterior) no es una operación
            .OrderByDescending(x => x.m.Id).Take(cantidad)
            .Select(x => new MovimientoInfo(x.m.FechaRegistro, x.t.Id, x.t.Codigo, x.m.EstadoAnteriorId, x.m.EstadoNuevoId, x.m.AtencionId))
            .ToListAsync(ct);
    }

    /// <summary>GET /tarjetas: las tarjetas de las sedes del usuario.</summary>
    public async Task<List<TarjetaDto>> ListarParaUsuarioAsync(CancellationToken ct)
    {
        var lista = await ListarAsync(await sedes.SedesDelUsuarioAsync(ct), ct);
        return [.. lista.Select(t => new TarjetaDto(t.Id, t.Codigo, t.Estado.Codigo(), t.Estado.Nombre(), t.Desde))];
    }

    /// <summary>F-09: Por desinfectar → Disponible.</summary>
    public async Task<TarjetaDto> DesinfectarAsync(int id, CancellationToken ct) =>
        (await DesinfectarAsync(new TandaTarjetasRequest([id]), ct))[0];

    /// <summary>F-09 por tandas: el operador limpia varias tarjetas juntas y las confirma de una vez.</summary>
    public Task<List<TarjetaDto>> DesinfectarAsync(TandaTarjetasRequest req, CancellationToken ct) =>
        TransicionDeUsuarioAsync(req, EstadoTarjeta.PorDesinfectar, EstadoTarjeta.Disponible, AccionAuditoria.Desinfectar,
            "ya no está «Por desinfectar»", ct);

    /// <summary>F-10: Sin devolver → Extraviada.</summary>
    public async Task<TarjetaDto> ExtraviarAsync(int id, CancellationToken ct) =>
        (await ExtraviarAsync(new TandaTarjetasRequest([id]), ct))[0];

    /// <summary>F-10 por tandas.</summary>
    public Task<List<TarjetaDto>> ExtraviarAsync(TandaTarjetasRequest req, CancellationToken ct) =>
        TransicionDeUsuarioAsync(req, EstadoTarjeta.SinDevolver, EstadoTarjeta.Extraviada, AccionAuditoria.Extraviar,
            "ya no está «Sin devolver»", ct);

    /// <summary>
    /// Cambia el estado de una tanda de tarjetas: todas o ninguna. Primero valida todas (existen, son de
    /// las sedes del usuario y están en el estado esperado); después guarda todo en un solo SaveChanges,
    /// que es una transacción. Cada tarjeta deja su movimiento y su fila de auditoría (RN-27).
    /// </summary>
    private async Task<List<TarjetaDto>> TransicionDeUsuarioAsync(TandaTarjetasRequest req, EstadoTarjeta desde, EstadoTarjeta hacia,
        AccionAuditoria accion, string conflicto, CancellationToken ct)
    {
        await validadorTanda.ValidarAsync(req, ct);
        var alcance = await sedes.SedesDelUsuarioAsync(ct);
        var encontradas = await db.Tarjetas.Where(t => req.TarjetaIds.Contains(t.Id)).ToListAsync(ct);
        var tarjetas = encontradas.Where(t => alcance is null || alcance.Contains(t.SedeId)).ToList();
        if (tarjetas.Count != req.TarjetaIds.Length)
            throw new NoEncontradoException(req.TarjetaIds.Length == 1 ? "La tarjeta no existe." : "Alguna de las tarjetas no existe.");

        var fuera = tarjetas.Where(t => t.EstadoTarjetaId != desde).Select(t => t.Codigo).ToList();
        if (fuera.Count > 0)
            throw new ConflictoException(
                fuera.Count == 1 ? $"La tarjeta {fuera[0]} {conflicto}." : $"Las tarjetas {string.Join(", ", fuera)} {conflicto}. No se cambió ninguna.",
                new { tarjetas = fuera });

        foreach (var t in tarjetas)
        {
            db.MovimientosTarjeta.Add(t.CambiarA(hacia));
            auditoria.Registrar(accion, "Tarjeta", t.Codigo, new { estado = desde.Codigo() }, new { estado = hacia.Codigo() });
        }
        await db.GuardarAsync(ct);
        var ahora = reloj.AhoraUtc;
        return [.. tarjetas.Select(t => new TarjetaDto(t.Id, t.Codigo, hacia.Codigo(), hacia.Nombre(), ahora))];
    }

    private async Task<TarjetaInfo> InfoAsync(Tarjeta t, CancellationToken ct)
    {
        var desde = await db.MovimientosTarjeta.Where(m => m.TarjetaId == t.Id).MaxAsync(m => (DateTime?)m.FechaRegistro, ct);
        return new TarjetaInfo(t.Id, t.Codigo, t.SedeId, t.EstadoTarjetaId, desde);
    }
}
