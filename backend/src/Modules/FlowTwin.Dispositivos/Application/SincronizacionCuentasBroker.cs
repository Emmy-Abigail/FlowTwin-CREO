using FlowTwin.Dispositivos.Domain;
using FlowTwin.Mensajeria.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowTwin.Dispositivos.Application;

/// <summary>
/// Al arrancar, deja las cuentas del broker de acuerdo con la base, que es la fuente de verdad:
///  - borra las cuentas de nodos que ya no existen o están de baja (por ejemplo, si el broker estaba
///    apagado cuando se dio de baja uno): ninguna cuenta huérfana queda abierta;
///  - avisa en el log de los nodos activos SIN cuenta: su clave no se guarda en ningún lado, así que no se
///    puede recrear; el Administrador debe regenerarla. La única excepción son los nodos de demostración
///    de la semilla, cuya clave es conocida (solo con BaseDatos:SembrarDemo).
/// Reintenta hasta que el broker responda.
/// </summary>
public sealed class SincronizacionCuentasBroker(
    IServiceScopeFactory scopes, ICuentasNodosBroker broker, IConfiguration config, ILogger<SincronizacionCuentasBroker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var espera = TimeSpan.FromSeconds(2);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SincronizarAsync(ct);
                return;
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                log.LogWarning("No se pudieron sincronizar las cuentas del broker ({Error}); reintento en {Espera} s", e.Message, espera.TotalSeconds);
                try { await Task.Delay(espera, ct); } catch (OperationCanceledException) { return; }
                espera = TimeSpan.FromSeconds(Math.Min(espera.TotalSeconds * 2, 60));
            }
        }
    }

    public async Task SincronizarAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDispositivosDb>();
        var activos = await db.Nodos.AsNoTracking().Where(n => n.EstadoNodoId != EstadoNodo.Baja).ToListAsync(ct);
        var conCuenta = (await broker.MacsConCuentaAsync(ct)).ToHashSet();

        foreach (var mac in conCuenta.Where(m => activos.All(n => n.Mac != m)))
        {
            await broker.EliminarAsync(mac, ct);
            log.LogWarning("Se borró la cuenta MQTT de {Mac}: no corresponde a ningún nodo activo", mac);
        }

        var demo = config.GetValue("BaseDatos:SembrarDemo", false);
        foreach (var nodo in activos.Where(n => !conCuenta.Contains(n.Mac)))
        {
            if (demo && nodo.ClaveCoincide(ClaveNodo.Demo(nodo.Codigo)))
                await broker.CrearAsync(nodo.Mac, ClaveNodo.Demo(nodo.Codigo), ct);
            else
                log.LogWarning("El nodo {Codigo} no tiene cuenta en el broker y no podrá conectarse: regenera su clave en Dispositivos", nodo.Codigo);
        }
    }
}
