using System.Text.RegularExpressions;
using FlowTwin.Mensajeria.Contracts;

namespace FlowTwin.Mensajeria.Application;

/// <summary>
/// Los temas y cuentas MQTT de un entorno (ADR-24). Es puro: se prueba sin broker.
///   {prefijo}/v1/nodos/{mac}/lecturas    lo publica el nodo
///   {prefijo}/v1/nodos/{mac}/latidos     lo publica el nodo
///   {prefijo}/v1/nodos/{mac}/respuestas  lo publica el backend; el nodo se suscribe
/// {mac} va en minúsculas y sin separadores (a4cf129b3e03): los «:» son válidos en un tema, pero así
/// el tema se lee y se escribe en C sin ambigüedades. El «v1» permite cambiar el contrato sin romper nodos.
/// </summary>
public sealed partial class TemasMqtt
{
    public const string Lecturas = "lecturas", Latidos = "latidos", Respuestas = "respuestas";

    public TemasMqtt(string prefijo)
    {
        if (!PrefijoValido().IsMatch(prefijo))
            throw new ArgumentException($"El prefijo MQTT «{prefijo}» no es válido: minúsculas, números y guiones, empezando con letra.");
        Prefijo = prefijo;
        Raiz = $"{prefijo}/v1/nodos";
    }

    public string Prefijo { get; }
    public string Raiz { get; }

    /// <summary>Cuenta del cliente interno del backend que recibe los mensajes de los nodos.</summary>
    public string UsuarioIngesta => $"{Prefijo}-ingesta";

    public string Tema(string mac, string tipo) => $"{Raiz}/{Segmento(mac)}/{tipo}";

    /// <summary>Lo que se suscribe el backend: un tipo de mensaje de todos los nodos.</summary>
    public string Filtro(string tipo) => $"{Raiz}/+/{tipo}";

    public string UsuarioNodo(string mac) => $"{Prefijo}-{Segmento(mac)}";

    public CuentaMqtt CuentaDe(string mac) =>
        new(UsuarioNodo(mac), Tema(mac, Lecturas), Tema(mac, Latidos), Tema(mac, Respuestas));

    /// <summary>De un tema recibido, la MAC (A4:CF:…) y el tipo. Null si no es de este entorno o no tiene la forma esperada.</summary>
    public (string Mac, string Tipo)? Leer(string tema)
    {
        var partes = tema.Split('/');
        var raiz = Raiz.Split('/');
        if (partes.Length != raiz.Length + 2 || !partes.Take(raiz.Length).SequenceEqual(raiz)) return null;
        var mac = MacDeSegmento(partes[^2]);
        return mac is null ? null : (mac, partes[^1]);
    }

    /// <summary>De una cuenta de nodo de este entorno, su MAC. Null si la cuenta es de otro entorno o no es de un nodo.</summary>
    public string? MacDeUsuario(string usuario) =>
        usuario.StartsWith(Prefijo + "-", StringComparison.Ordinal) ? MacDeSegmento(usuario[(Prefijo.Length + 1)..]) : null;

    /// <summary>A4:CF:12:9B:3E:03 → a4cf129b3e03.</summary>
    public static string Segmento(string mac) => mac.Replace(":", "").ToLowerInvariant();

    /// <summary>a4cf129b3e03 → A4:CF:12:9B:3E:03. Null si no son 12 dígitos hexadecimales en minúscula.</summary>
    public static string? MacDeSegmento(string segmento) =>
        SegmentoValido().IsMatch(segmento)
            ? string.Join(':', Enumerable.Range(0, 6).Select(i => segmento.Substring(i * 2, 2).ToUpperInvariant()))
            : null;

    [GeneratedRegex("^[a-z][a-z0-9-]{0,30}$")]
    private static partial Regex PrefijoValido();

    [GeneratedRegex("^[0-9a-f]{12}$")]
    private static partial Regex SegmentoValido();
}
