using System.Collections.Concurrent;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FlowTwin.Monitoreo.Endpoints;

/// <summary>
/// Hub /hubs/gemelo (sección "Hub SignalR"). El navegador se une al grupo de un piso y desde ahí recibe
/// los cambios. Solo puede unirse a los pisos que tiene asignados (RN-24): el permiso se revisa aquí,
/// en el servidor, no en la pantalla.
/// </summary>
[Authorize]
public sealed class GemeloHub(SuscripcionesPiso suscripciones) : Hub
{
    public static string Grupo(int pisoId) => $"piso-{pisoId}";

    public async Task UnirsePiso(int pisoId)
    {
        if (!Context.User.PuedeVerPiso(pisoId)) throw new HubException("No tienes asignado este piso.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(pisoId));
        suscripciones.Agregar(Context.ConnectionId, pisoId);
    }

    public async Task SalirPiso(int pisoId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(pisoId));
        suscripciones.Quitar(Context.ConnectionId, pisoId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        suscripciones.QuitarConexion(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Qué pisos está mirando alguien ahora. El notificador solo calcula esos: si nadie mira el gemelo,
/// no consulta la base.
/// </summary>
public sealed class SuscripcionesPiso
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> porConexion = new();

    public void Agregar(string conexion, int pisoId) => porConexion.GetOrAdd(conexion, _ => new())[pisoId] = 0;

    public void Quitar(string conexion, int pisoId)
    {
        if (porConexion.TryGetValue(conexion, out var pisos)) pisos.TryRemove(pisoId, out _);
    }

    public void QuitarConexion(string conexion) => porConexion.TryRemove(conexion, out _);

    public IReadOnlySet<int> PisosMirados() => porConexion.Values.SelectMany(p => p.Keys).ToHashSet();
}
