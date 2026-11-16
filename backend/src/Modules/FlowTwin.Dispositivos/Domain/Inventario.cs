using System.Linq.Expressions;
using FlowTwin.Dispositivos.Contracts;

namespace FlowTwin.Dispositivos.Domain;

/// <summary>
/// P9 · Dispositivos, por excepción: qué tarjetas y nodos «requieren atención» del Administrador. Las reglas
/// están una sola vez como expresiones: la misma sirve para filtrar en SQL y para las pruebas.
/// Las tarjetas Sin devolver no están aquí: las atiende el Operador en Admisión (no se duplica su trabajo).
/// </summary>
public static class ReglasInventario
{
    /// <summary>
    /// Sin detectarse por este tiempo, una tarjeta guardada en recepción (al alcance del nodo de Admisión)
    /// probablemente no tiene batería. Un día evita el aviso falso de una noche apagada cargando.
    /// </summary>
    public static readonly TimeSpan SinDeteccion = TimeSpan.FromHours(24);

    public const string Extraviada = "EXTRAVIADA", NuncaDetectada = "NUNCA_DETECTADA", SinDeteccionReciente = "SIN_DETECCION";

    /// <summary>Estados en que la tarjeta está guardada en recepción y debería detectarse.</summary>
    private static readonly EstadoTarjeta[] Guardadas = [EstadoTarjeta.Disponible, EstadoTarjeta.PorDesinfectar];

    /// <summary>Extraviada (decidir si se da de baja), o guardada y sin detectarse hace más de un día.</summary>
    public static Expression<Func<Tarjeta, bool>> TarjetaRequiereAtencion(DateTime ahora)
    {
        var limite = ahora - SinDeteccion;
        return t => t.EstadoTarjetaId == EstadoTarjeta.Extraviada
            || ((t.EstadoTarjetaId == EstadoTarjeta.Disponible || t.EstadoTarjetaId == EstadoTarjeta.PorDesinfectar)
                && ((t.UltimaDeteccionEn == null && t.FechaRegistro < limite) || t.UltimaDeteccionEn < limite));
    }

    /// <summary>El motivo de una tarjeta que requiere atención (null si no requiere).</summary>
    public static string? MotivoTarjeta(EstadoTarjeta estado, DateTime? ultimaDeteccion, DateTime registrada, DateTime ahora)
    {
        if (estado == EstadoTarjeta.Extraviada) return Extraviada;
        if (!Guardadas.Contains(estado)) return null;
        var limite = ahora - SinDeteccion;
        if (ultimaDeteccion is null) return registrada < limite ? NuncaDetectada : null;
        return ultimaDeteccion < limite ? SinDeteccionReciente : null;
    }

    /// <summary>Un nodo que no está de baja y no envía latidos (RN-20).</summary>
    public static Expression<Func<Nodo, bool>> NodoRequiereAtencion() => n => n.EstadoNodoId == EstadoNodo.SinSenal;
}
