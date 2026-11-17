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
    IAtencionesCerradas cerradas, IPausasZonas pausas, IUsuarioActual usuario, IReloj reloj, ZonaHoraria zona,
    IParametrosSistema parametros)
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
        var datos = await DatosAsync(pisoId, ct);
        return datos?.Estado;
    }

    /// <summary>GET /pisos/{id}/alertas (ADR-25): Operador y Administrador, de sus pisos (RN-24).</summary>
    public async Task<IReadOnlyList<AlertaPiso>> AlertasAsync(int pisoId, CancellationToken ct)
    {
        if (!usuario.PuedeVerPiso(pisoId)) throw new SinPermisoException("No tienes asignado este piso.");
        var datos = await DatosAsync(pisoId, ct)
            ?? throw new NoEncontradoException($"No existe el piso {pisoId} o aún no está publicado.");
        var p = await parametros.ObtenerAsync(ct);
        var opciones = new OpcionesAlertas { EsperaAltaMin = p.EsperaAltaMin, EsperaCriticaMin = p.EsperaCriticaMin, SinLecturasMin = p.SinLecturasMin };
        return CalculoAlertas.Calcular(datos.Estado, datos.Atenciones, opciones, reloj.AhoraUtc);
    }

    private async Task<(EstadoPiso Estado, IReadOnlyList<AtencionEnCurso> Atenciones)?> DatosAsync(int pisoId, CancellationToken ct)
    {
        var piso = await zonas.PisoAsync(pisoId, ct);
        if (piso is null) return null;
        var zonasPiso = await zonas.DelPisoAsync(pisoId, ct);
        var enCurso = await atenciones.DeSedeAsync(piso.SedeId, ct);
        var estado = CalculoEstado.Calcular(pisoId, zonasPiso, await dispositivos.NodosAsync(ct), enCurso, reloj.AhoraUtc,
            await pausas.AbiertasAsync([.. zonasPiso.Select(z => z.Id)], ct), (await parametros.ObtenerAsync(ct)).SemaforoAmbarPct);
        return (estado, enCurso);
    }

    /// <summary>GET /pisos/{id}/atendidas-hoy: las atenciones terminadas hoy en la sede del piso (RN-24).</summary>
    public async Task<IReadOnlyList<AtendidaHoy>> AtendidasHoyAsync(int pisoId, CancellationToken ct)
    {
        if (!usuario.PuedeVerPiso(pisoId)) throw new SinPermisoException("No tienes asignado este piso.");
        var piso = await zonas.PisoAsync(pisoId, ct)
            ?? throw new NoEncontradoException($"No existe el piso {pisoId} o aún no está publicado.");
        var hoy = zona.FechaLocal(reloj.AhoraUtc);
        return CalculoEstado.Atendidas(await zonas.DelPisoAsync(pisoId, ct), await cerradas.DelDiaAsync(piso.SedeId, hoy, ct));
    }
}
