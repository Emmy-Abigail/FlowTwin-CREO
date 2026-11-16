using FlowTwin.SharedKernel;

namespace FlowTwin.Mensajeria.Contracts;

/// <summary>
/// Lo que llega de un nodo por MQTT. Mac: la del tema, en formato A4:CF:12:9B:3E:03. Es confiable porque
/// el broker solo deja publicar en ese tema al usuario de ese nodo (ADR-24). Contenido: el JSON tal cual.
/// </summary>
public sealed record MensajeNodo(string Mac, ReadOnlyMemory<byte> Contenido);

/// <summary>
/// Lo que el backend le contesta al nodo en su tema «respuestas» (contrato de integración, sección 6).
/// Solo con GUARDADO o DUPLICADO el nodo borra el lote de su búfer: es el equivalente al 202 de antes.
/// </summary>
public sealed record RespuestaNodo(string Tipo, string Estado, long? Secuencia = null, IDictionary<string, string[]>? Errores = null)
{
    /// <summary>Se guardó (o, en un latido, se registró).</summary>
    public const string Guardado = "GUARDADO";
    /// <summary>Ese lote ya había llegado: no se guardó otra vez, pero para el nodo es un éxito.</summary>
    public const string Duplicado = "DUPLICADO";
    /// <summary>El JSON no cumple el contrato: no reintentar (fallaría igual). Trae los errores.</summary>
    public const string Invalido = "INVALIDO";
    /// <summary>El nodo no está registrado o está de baja: dejar de enviar y avisar (LED).</summary>
    public const string Rechazado = "RECHAZADO";
    /// <summary>Envía más mensajes de los permitidos: esperar y reintentar.</summary>
    public const string Limite = "LIMITE";
    /// <summary>Falló el servidor (por ejemplo, la base): conservar el lote y reintentar.</summary>
    public const string Error = "ERROR";

    /// <summary>
    /// Traduce un error del dominio a lo que el nodo debe hacer, en un solo lugar para todos los tipos:
    /// datos inválidos → INVALIDO con sus errores; nodo no reconocido → RECHAZADO; lo demás → ERROR (reintentar).
    /// </summary>
    public static RespuestaNodo DesdeError(string tipo, ErrorFlowTwin error, long? secuencia = null) => error switch
    {
        ValidacionException v => new(tipo, Invalido, secuencia, v.Errores),
        NoAutenticadoException => new(tipo, Rechazado, secuencia),
        _ when error.EstadoHttp == 400 => new(tipo, Invalido, secuencia, new Dictionary<string, string[]> { ["mensaje"] = [error.Message] }),
        _ => new(tipo, Error, secuencia),
    };
}

/// <summary>
/// Un tipo de mensaje de los nodos («lecturas», «latidos») y quién lo procesa. Cada módulo registra el suyo:
/// Mensajeria transporta y no sabe qué significan.
/// </summary>
public interface IManejadorNodo
{
    /// <summary>Último segmento del tema: flowtwin/v1/nodos/{mac}/{Tipo}.</summary>
    string Tipo { get; }

    Task<RespuestaNodo> ManejarAsync(MensajeNodo mensaje, CancellationToken ct);
}

/// <summary>Usuario y temas MQTT de un nodo: se muestran al registrarlo, junto con su clave, para cargarlos en su NVS.</summary>
public sealed record CuentaMqtt(string Usuario, string TemaLecturas, string TemaLatidos, string TemaRespuestas);

/// <summary>
/// Las cuentas de los nodos en el broker (plugin Dynamic Security de Mosquitto). La clave solo se conoce al
/// registrar o regenerar: en ese momento Dispositivos la entrega aquí y el broker guarda su propio hash.
/// </summary>
public interface ICuentasNodosBroker
{
    /// <summary>Crea la cuenta del nodo, o la reemplaza si ya existía (la sesión con la clave anterior se cierra).</summary>
    Task<CuentaMqtt> CrearAsync(string mac, string clave, CancellationToken ct = default);

    /// <summary>Borra la cuenta: el nodo queda desconectado y ya no puede volver a entrar. No falla si no existía.</summary>
    Task EliminarAsync(string mac, CancellationToken ct = default);

    /// <summary>Las MAC de los nodos que tienen cuenta en el broker (de este entorno).</summary>
    Task<IReadOnlyList<string>> MacsConCuentaAsync(CancellationToken ct = default);

    /// <summary>Usuario y temas que le corresponden a esa MAC (no consulta al broker).</summary>
    CuentaMqtt CuentaDe(string mac);
}
