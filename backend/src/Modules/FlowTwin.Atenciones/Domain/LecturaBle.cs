using FlowTwin.SharedKernel;

namespace FlowTwin.Atenciones.Domain;

/// <summary>
/// ope.LecturaBle: una lectura cruda de un nodo («el nodo vio este emisor con esta señal»). Es la
/// materia prima del motor de presencia y de la pantalla de diagnóstico. Inmutable; FechaRegistro es la
/// hora en que llegó al servidor. Se guarda con la señal tal como la midió el nodo: el ajuste de
/// calibración (AjusteRssiDb) lo aplica el motor, así un cambio de calibración no altera el historial.
/// Se purgan a los 7 días (DD-11).
/// </summary>
public class LecturaBle : EntidadEvento
{
    public long Id { get; set; }
    public int NodoId { get; set; }
    /// <summary>Identificador del emisor; puede no estar registrado (lo usa F-16 para detectarlo).</summary>
    public string UuidBle { get; set; } = "";
    public short Rssi { get; set; }
    /// <summary>Cuándo la vio el nodo, según el reloj del SERVIDOR (ADR-14).</summary>
    public DateTime LeidaEn { get; set; }

    /// <summary>
    /// El ESP32 no envía la hora, solo hace cuántos ms vio al emisor: LeidaEn = recepción - edadMs.
    /// Así no importa que el reloj del nodo esté desfasado.
    /// </summary>
    public static LecturaBle Desde(int nodoId, string uuidBle, int rssi, int edadMs, DateTime recibidaEn) => new()
    {
        NodoId = nodoId,
        UuidBle = NormalizarUuid(uuidBle),
        Rssi = (short)rssi,
        LeidaEn = recibidaEn.AddMilliseconds(-edadMs),
    };

    /// <summary>Minúsculas y sin espacios, para que el mismo emisor no aparezca como dos distintos.</summary>
    public static string NormalizarUuid(string uuid) => uuid.Trim().ToLowerInvariant();
}
