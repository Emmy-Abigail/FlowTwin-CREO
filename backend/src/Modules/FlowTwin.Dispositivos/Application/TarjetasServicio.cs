using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
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

/// <summary>Tarjetas BLE: consultas, desinfección (F-09) y extravío (F-10).</summary>
public sealed class TarjetasServicio(IDispositivosDb db, ISedesConsultas sedes, IAuditoria auditoria, IReloj reloj) : ITarjetasAdmision
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

    /// <summary>F-09: Por desinfectar → Disponible. Otro estado responde 409.</summary>
    public Task<TarjetaDto> DesinfectarAsync(int id, CancellationToken ct) =>
        TransicionDeUsuarioAsync(id, EstadoTarjeta.PorDesinfectar, EstadoTarjeta.Disponible, AccionAuditoria.Desinfectar,
            "La tarjeta ya no está «Por desinfectar».", ct);

    /// <summary>F-10: Sin devolver → Extraviada.</summary>
    public Task<TarjetaDto> ExtraviarAsync(int id, CancellationToken ct) =>
        TransicionDeUsuarioAsync(id, EstadoTarjeta.SinDevolver, EstadoTarjeta.Extraviada, AccionAuditoria.Extraviar,
            "La tarjeta ya no está «Sin devolver».", ct);

    private async Task<TarjetaDto> TransicionDeUsuarioAsync(int id, EstadoTarjeta desde, EstadoTarjeta hacia,
        AccionAuditoria accion, string mensajeConflicto, CancellationToken ct)
    {
        var alcance = await sedes.SedesDelUsuarioAsync(ct);
        var tarjeta = await db.Tarjetas.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (tarjeta is null || (alcance is not null && !alcance.Contains(tarjeta.SedeId)))
            throw new NoEncontradoException("La tarjeta no existe.");
        if (tarjeta.EstadoTarjetaId != desde)
            throw new ConflictoException(mensajeConflicto, new { estado = tarjeta.EstadoTarjetaId.Codigo() });

        db.MovimientosTarjeta.Add(tarjeta.CambiarA(hacia));
        auditoria.Registrar(accion, "Tarjeta", tarjeta.Codigo, new { estado = desde.Codigo() }, new { estado = hacia.Codigo() });
        await db.GuardarAsync(ct);
        return new TarjetaDto(tarjeta.Id, tarjeta.Codigo, hacia.Codigo(), hacia.Nombre(), reloj.AhoraUtc);
    }

    private async Task<TarjetaInfo> InfoAsync(Tarjeta t, CancellationToken ct)
    {
        var desde = await db.MovimientosTarjeta.Where(m => m.TarjetaId == t.Id).MaxAsync(m => (DateTime?)m.FechaRegistro, ct);
        return new TarjetaInfo(t.Id, t.Codigo, t.SedeId, t.EstadoTarjetaId, desde);
    }
}
