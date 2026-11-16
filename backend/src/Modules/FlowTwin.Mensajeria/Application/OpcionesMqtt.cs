namespace FlowTwin.Mensajeria.Application;

/// <summary>
/// Sección «Mqtt» de appsettings. La clave del administrador del broker es un secreto: va en user-secrets
/// (desarrollo) o en una variable de entorno (Mqtt__ClaveAdmin), nunca en el repositorio.
/// </summary>
public sealed class OpcionesMqtt
{
    /// <summary>Apagado, la API arranca sin broker (por ejemplo, para correr solo pantallas). La ingesta no funciona.</summary>
    public bool Activo { get; set; } = true;
    public string Servidor { get; set; } = "localhost";
    public int Puerto { get; set; } = 1883;
    /// <summary>En la clínica, siempre TLS (puerto 8883).</summary>
    public bool UsarTls { get; set; }
    /// <summary>
    /// Raíz de los temas y prefijo de las cuentas. Separa entornos que comparten broker en desarrollo
    /// (trabajo, simulador, pruebas): cada uno solo ve y administra lo suyo.
    /// </summary>
    public string Prefijo { get; set; } = "flowtwin";
    public string UsuarioAdmin { get; set; } = "flowtwin-admin";
    public string? ClaveAdmin { get; set; }
    /// <summary>
    /// Tope de mensajes por nodo y minuto: protege de un firmware descontrolado. Uno normal envía ~32; el
    /// margen cubre el reenvío de 10 minutos de lotes guardados tras un corte de Wi-Fi.
    /// </summary>
    public int MensajesPorMinutoPorNodo { get; set; } = 300;
    /// <summary>Espera máxima entre reintentos de conexión con el broker.</summary>
    public int ReintentoMaximoSegundos { get; set; } = 30;
}
