using FlowTwin.Atenciones.Domain;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Atenciones.Application;

public sealed record PausarZonaRequest(string Motivo, string? Detalle);

/// <summary>La zona fuera de servicio, tal como la devuelven las acciones.</summary>
public sealed record PausaDto(int ZonaId, string Motivo, string? Detalle, DateTime InicioEn);

/// <summary>
/// RF09 (ADR-25): poner una zona fuera de servicio y volver a ponerla en servicio, desde la ventana de zona
/// del gemelo. Operador y Administrador, solo en sus pisos (RN-24). Queda en la auditoría.
/// </summary>
public sealed class ZonasEnServicio(IAtencionesDb db, IZonasConsultas zonas, IUsuarioActual usuario, IAuditoria auditoria, IReloj reloj)
{
    public async Task<PausaDto> PausarAsync(int zonaId, PausarZonaRequest req, CancellationToken ct)
    {
        var zona = await ZonaPermitidaAsync(zonaId, ct);
        var motivo = CatalogoExtensiones.DesdeCodigo<MotivoPausa>(req.Motivo)
            ?? throw new ReglaNegocioException("PAUSA_MOTIVO_INVALIDO", "Elige un motivo de la lista.");
        if (await db.PausasZona.AnyAsync(p => p.ZonaId == zonaId && p.FinEn == null, ct))
            throw new ConflictoException($"{zona.Nombre} ya está fuera de servicio.");

        var pausa = PausaZona.Iniciar(zonaId, motivo, req.Detalle, usuario.Id, reloj.AhoraUtc);
        db.PausasZona.Add(pausa);
        auditoria.Registrar(AccionAuditoria.Pausar, "Zona", zona.Nombre, despues: new { motivo = motivo.Codigo(), detalle = pausa.Detalle });
        try { await db.GuardarAsync(ct); }
        catch (DbUpdateException) { throw new ConflictoException($"{zona.Nombre} ya está fuera de servicio."); }   // dos clics a la vez: lo para el índice único
        return new PausaDto(zonaId, motivo.Codigo(), pausa.Detalle, pausa.InicioEn);
    }

    public async Task ReanudarAsync(int zonaId, CancellationToken ct)
    {
        var zona = await ZonaPermitidaAsync(zonaId, ct);
        var pausa = await db.PausasZona.SingleOrDefaultAsync(p => p.ZonaId == zonaId && p.FinEn == null, ct)
            ?? throw new ConflictoException($"{zona.Nombre} ya estaba en servicio.");
        pausa.Finalizar(reloj.AhoraUtc);
        auditoria.Registrar(AccionAuditoria.Reanudar, "Zona", zona.Nombre, new { motivo = pausa.MotivoPausaId.Codigo() });
        await db.GuardarAsync(ct);
    }

    private async Task<ZonaInfo> ZonaPermitidaAsync(int zonaId, CancellationToken ct)
    {
        var zona = await zonas.ZonaAsync(zonaId, ct) ?? throw new NoEncontradoException("La zona no existe.");
        if (!usuario.PuedeVerPiso(zona.PisoId)) throw new SinPermisoException("No tienes asignado el piso de esta zona.");
        return zona;
    }
}
