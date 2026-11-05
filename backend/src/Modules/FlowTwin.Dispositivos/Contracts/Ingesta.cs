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

/// <summary>
/// Un nodo que no está de baja: la zona que cubre, su ajuste de calibración (ADR-20) y su estado
/// (EN_LINEA o SIN_SENAL). Una zona sin ningún nodo En línea no tiene datos confiables.
/// </summary>
public sealed record NodoZona(int Id, string Codigo, int ZonaId, short AjusteRssiDb, string Estado, DateTime? UltimoLatidoEn)
{
    public bool EnLinea => Estado == "EN_LINEA";
}

/// <summary>Lo que el motor de presencia (Atenciones) necesita saber de los dispositivos.</summary>
public interface IPresenciaDispositivos
{
    /// <summary>Nodos que no están de baja, con su zona.</summary>
    Task<IReadOnlyList<NodoZona>> NodosAsync(CancellationToken ct = default);

    /// <summary>Identificador BLE (en minúsculas) de cada tarjeta pedida: TarjetaId → uuidBle.</summary>
    Task<IReadOnlyDictionary<int, string>> UuidsDeTarjetasAsync(IReadOnlyCollection<int> tarjetaIds, CancellationToken ct = default);
}
