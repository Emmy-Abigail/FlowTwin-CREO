using FlowTwin.Atenciones.Contracts;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.Monitoreo.Domain;
using FlowTwin.SharedKernel;

namespace FlowTwin.Monitoreo.Application;

/// <summary>
/// Arma la foto del piso preguntando a cada módulo por su contrato (zonas a Gemelo, nodos a Dispositivos,
/// atenciones a Atenciones) y la calcula con CalculoEstado. Se calcula desde la base cada vez: no hay un
/// estado guardado aparte que pueda desalinearse.
/// </summary>
public sealed class EstadoPisoServicio(
    IZonasConsultas zonas, IPresenciaDispositivos dispositivos, IAtencionesEnCurso atenciones,
    IUsuarioActual usuario, IReloj reloj)
{
    /// <summary>GET /pisos/{id}/estado (F-11): con permiso del piso (RN-24).</summary>
    public async Task<EstadoPiso> ParaUsuarioAsync(int pisoId, CancellationToken ct)
    {
        if (!usuario.PuedeVerPiso(pisoId)) throw new SinPermisoException("No tienes asignado este piso.");
        return await CalcularAsync(pisoId, ct)
            ?? throw new NoEncontradoException($"No existe el piso {pisoId} o aún no está publicado.");
    }

    /// <summary>Sin revisar permisos: lo usa el notificador, que ya filtró quién está suscrito. null si el piso no existe.</summary>
    public async Task<EstadoPiso?> CalcularAsync(int pisoId, CancellationToken ct)
    {
        var piso = await zonas.PisoAsync(pisoId, ct);
        if (piso is null) return null;
        return CalculoEstado.Calcular(pisoId,
            await zonas.DelPisoAsync(pisoId, ct),
            await dispositivos.NodosAsync(ct),
            await atenciones.DeSedeAsync(piso.SedeId, ct),
            reloj.AhoraUtc);
    }
}
