using FlowTwin.SharedKernel;

namespace FlowTwin.Dispositivos.Contracts;

/// <summary>
/// Catálogo cat.EstadoTarjeta. Está en Contracts porque Atenciones también lo usa
/// (es parte del lenguaje común entre los dos módulos).
/// </summary>
public enum EstadoTarjeta
{
    [Catalogo("DISPONIBLE", "Disponible")] Disponible = 1,
    [Catalogo("EN_USO", "En uso")] EnUso = 2,
    [Catalogo("POR_DESINFECTAR", "Por desinfectar")] PorDesinfectar = 3,
    [Catalogo("SIN_DEVOLVER", "Sin devolver")] SinDevolver = 4,
    [Catalogo("EXTRAVIADA", "Extraviada")] Extraviada = 5,
    [Catalogo("BAJA", "De baja")] Baja = 6,
}

public sealed record TarjetaInfo(int Id, string Codigo, int SedeId, EstadoTarjeta Estado, DateTime? Desde);

public sealed record MovimientoInfo(
    DateTime Fecha, int TarjetaId, string TarjetaCodigo, EstadoTarjeta? Anterior, EstadoTarjeta Nuevo, long? AtencionId);

/// <summary>Lo que Atenciones puede pedirle a Dispositivos sobre las tarjetas.</summary>
public interface ITarjetasAdmision
{
    /// <summary>Busca por el contenido del QR o por el código ("T-07", "t7" o "7"). null si no existe.</summary>
    Task<TarjetaInfo?> BuscarAsync(string codigoOQr, CancellationToken ct = default);

    /// <summary>
    /// Cambia el estado validando la máquina de estados y agrega su fila en MovimientoTarjeta.
    /// No guarda: lo guarda quien llama, junto con su propio cambio (una sola transacción).
    /// </summary>
    Task<TarjetaInfo> CambiarEstadoAsync(int tarjetaId, EstadoTarjeta nuevo, long? atencionId = null,
        string? observacion = null, CancellationToken ct = default);

    /// <summary>Tarjetas de esas sedes (todas si sedeIds es null) con el momento de su último cambio.</summary>
    Task<List<TarjetaInfo>> ListarAsync(IReadOnlyCollection<int>? sedeIds, CancellationToken ct = default);

    /// <summary>
    /// Las últimas operaciones de mostrador con tarjetas (entregar, recibir, desinfectar…). Excluye el
    /// alta de la tarjeta en el inventario: es tarea del Administrador, no una operación de Admisión.
    /// </summary>
    Task<List<MovimientoInfo>> UltimasOperacionesAsync(IReadOnlyCollection<int>? sedeIds, int cantidad, CancellationToken ct = default);
}
