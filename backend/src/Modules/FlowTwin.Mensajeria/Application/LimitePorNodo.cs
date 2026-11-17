using System.Collections.Concurrent;
using FlowTwin.SharedKernel;

namespace FlowTwin.Mensajeria.Application;

/// <summary>
/// Ventana fija de un minuto por nodo: un firmware con un error que envía sin parar no satura la base.
/// Mosquitto no limita mensajes por cliente, por eso se hace aquí, antes de procesar.
/// </summary>
public sealed class LimitePorNodo(IReloj reloj, int porMinuto)
{
    private readonly ConcurrentDictionary<string, (DateTime Ventana, int Cuenta)> cuentas = new();

    /// <summary>True si el mensaje entra en el límite (y lo cuenta).</summary>
    public bool Permitir(string mac)
    {
        var ahora = reloj.AhoraUtc;
        var ventana = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0, DateTimeKind.Utc);
        var (_, n) = cuentas.AddOrUpdate(mac, (ventana, 1), (_, actual) => actual.Ventana == ventana ? (ventana, actual.Cuenta + 1) : (ventana, 1));
        return n <= porMinuto;
    }
}
