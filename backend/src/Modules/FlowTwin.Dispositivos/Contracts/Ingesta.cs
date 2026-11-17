namespace FlowTwin.Dispositivos.Contracts;

/// <summary>
/// Un nodo activo (no dado de baja) que envía datos. Su identidad ya la comprobó el broker MQTT: solo la
/// cuenta de ese nodo puede publicar en el tema de su MAC (ADR-24).
/// </summary>
public sealed record NodoAutenticado(int Id, string Codigo, int ZonaId, short AjusteRssiDb);

/// <summary>Lo que la ingesta de lecturas (Atenciones) necesita saber del nodo que publicó.</summary>
public interface INodosIngesta
{
    /// <summary>
    /// El nodo con esa MAC si está registrado y no está de baja; null si no. Es una segunda barrera:
    /// al dar de baja un nodo también se borra su cuenta del broker, pero no se confía solo en eso.
    /// </summary>
    Task<NodoAutenticado?> ActivoPorMacAsync(string mac, CancellationToken ct = default);
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
