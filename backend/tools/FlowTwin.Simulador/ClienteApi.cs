using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FlowTwin.Simulador;

// DTO de la API, repetidos A PROPÓSITO (el simulador no referencia el backend): si la API cambia
// sin avisar, el simulador falla y lo delata, igual que le pasaría al firmware.
public sealed record ZonaApi(int Id, string Nombre, string Tipo, double X, double Y, double Ancho, double Fondo);
public sealed record NodoApi(string Codigo, double X, double Y, int ZonaId);
public sealed record GeometriaApi(int PisoId, string Nombre, List<ZonaApi> Zonas, List<NodoApi> Nodos);
public sealed record TarjetaApi(int Id, string Codigo, string Estado);
public sealed record AtencionApi(long Id, string Codigo);
public sealed record EscaneoApi(string Operacion, TarjetaApi Tarjeta, AtencionApi? Atencion);
public sealed record LecturaApi(string UuidBle, int Rssi, int EdadMs);

/// <summary>
/// Habla con la API como el Operador de recepción (JWT): entrega y devuelve tarjetas. Los nodos no usan
/// HTTP: van por MQTT (NodosMqtt).
/// </summary>
public sealed class ClienteApi(HttpClient http, Escenario escenario)
{
    private string? token;

    // ---- Como el Operador (recepción)

    public async Task<GeometriaApi> GeometriaAsync(CancellationToken ct) =>
        (await (await ComoOperadorAsync(HttpMethod.Get, $"/api/v1/pisos/{escenario.PisoId}/geometria", null, ct))
            .Content.ReadFromJsonAsync<GeometriaApi>(ct))!;

    public async Task<List<TarjetaApi>> TarjetasAsync(CancellationToken ct) =>
        (await (await ComoOperadorAsync(HttpMethod.Get, "/api/v1/tarjetas", null, ct)).Content.ReadFromJsonAsync<List<TarjetaApi>>(ct))!;

    /// <summary>Escanea el QR en Admisión: entrega si está Disponible, devuelve si está En uso (F-06, F-08).</summary>
    public async Task<EscaneoApi> EscanearAsync(string codigo, CancellationToken ct) =>
        (await (await ComoOperadorAsync(HttpMethod.Post, "/api/v1/admision/escaneos", new { codigoQr = codigo, modo = "AUTO" }, ct))
            .Content.ReadFromJsonAsync<EscaneoApi>(ct))!;

    public async Task DesinfectarAsync(int tarjetaId, CancellationToken ct) =>
        await ComoOperadorAsync(HttpMethod.Post, $"/api/v1/tarjetas/{tarjetaId}/desinfectar", null, ct);

    /// <summary>El token dura 15 min: ante un 401 se vuelve a iniciar sesión una vez y se reintenta.</summary>
    private async Task<HttpResponseMessage> ComoOperadorAsync(HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        for (var intento = 0; ; intento++)
        {
            token ??= await IniciarSesionAsync(ct);
            using var peticion = new HttpRequestMessage(metodo, ruta) { Content = cuerpo is null ? null : JsonContent.Create(cuerpo) };
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var r = await http.SendAsync(peticion, ct);
            if (r.StatusCode == HttpStatusCode.Unauthorized && intento == 0) { token = null; continue; }
            if (!r.IsSuccessStatusCode)
                throw new InvalidOperationException($"{metodo} {ruta} respondió {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync(ct)}");
            return r;
        }
    }

    private async Task<string> IniciarSesionAsync(CancellationToken ct)
    {
        var r = await http.PostAsJsonAsync("/api/v1/auth/login", new { correo = escenario.CorreoOperador, clave = escenario.ClaveOperador }, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<SesionApi>(ct))!.AccessToken;
    }

    private sealed record SesionApi(string AccessToken);
}
