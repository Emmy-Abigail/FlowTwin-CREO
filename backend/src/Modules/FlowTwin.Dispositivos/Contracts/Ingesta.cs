namespace FlowTwin.Dispositivos.Contracts;

/// <summary>El nodo que envió la petición, ya verificado.</summary>
public sealed record NodoAutenticado(int Id, string Codigo, int ZonaId, short AjusteRssiDb);

/// <summary>
/// Autenticación de los ESP32 para la ingesta (sección Seguridad → Nodos). La usa Atenciones al recibir
/// lecturas; los nodos son de Dispositivos, así que Atenciones no lee dis.Nodo directamente.
/// </summary>
public interface IAutenticacionNodos
{
    /// <summary>
    /// Verifica que la clave (cabecera X-Node-Key) sea la de ese nodo y que no esté de baja.
    /// Si algo no coincide lanza NoAutenticadoException (401) sin decir qué falló.
    /// </summary>
    Task<NodoAutenticado> AutenticarAsync(string? clave, string? mac, CancellationToken ct = default);
}
