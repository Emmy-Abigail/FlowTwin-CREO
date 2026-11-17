using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.Reportes.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.Reportes.Application;

/// <summary>
/// Detalle de una atención (P6): pide cada dato a su módulo por contrato y lo arma con CalculoDetalle.
/// </summary>
public sealed class DetalleAtencionServicio(
    IAtencionesConsulta atenciones, IZonasConsultas zonas, ISedesConsultas sedes, ITarjetasConsultas tarjetas,
    IReloj reloj)
{
    /// <summary>
    /// RN-24: solo si la atención es de una sede del usuario (el Administrador ve todas). Si no, responde
    /// lo mismo que si no existiera: un 404 no revela que hay una atención con ese número.
    /// </summary>
    public async Task<AtencionDetalle> ParaUsuarioAsync(long id, CancellationToken ct)
    {
        var a = await atenciones.PorIdAsync(id, ct);
        var visibles = await sedes.SedesDelUsuarioAsync(ct);
        if (a is null || (visibles is not null && !visibles.Contains(a.SedeId)))
            throw new NoEncontradoException($"No existe la atención {id} o no es de tus sedes.");

        var zonasRecorrido = await zonas.ParaRecorridoAsync([.. a.Estadias.Select(e => e.ZonaId).Distinct()], ct);
        var sede = await sedes.NombreSedeAsync(a.SedeId, ct) ?? $"Sede {a.SedeId}";
        var tarjeta = await tarjetas.CodigoAsync(a.TarjetaId, ct) ?? $"Tarjeta {a.TarjetaId}";
        return CalculoDetalle.Construir(a, zonasRecorrido, sede, tarjeta, reloj.AhoraUtc);
    }
}
