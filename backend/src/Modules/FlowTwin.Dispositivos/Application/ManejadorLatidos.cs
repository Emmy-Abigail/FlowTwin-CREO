using System.Text.Json;
using FlowTwin.Mensajeria.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.Dispositivos.Application;

/// <summary>Latidos que llegan por MQTT a {prefijo}/v1/nodos/{mac}/latidos (F-15).</summary>
public sealed class ManejadorLatidos(IngestaNodosServicio ingesta) : IManejadorNodo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Tipo => "latidos";

    public async Task<RespuestaNodo> ManejarAsync(MensajeNodo mensaje, CancellationToken ct)
    {
        LatidoRequest? latido;
        try { latido = JsonSerializer.Deserialize<LatidoRequest>(mensaje.Contenido.Span, Json); }
        catch (JsonException) { latido = null; }
        if (latido is null)
            return new RespuestaNodo(Tipo, RespuestaNodo.Invalido, Errores: new Dictionary<string, string[]> { ["json"] = ["El latido no es un JSON válido."] });
        try
        {
            await ingesta.RegistrarLatidoAsync(mensaje.Mac, latido, ct);
            return new RespuestaNodo(Tipo, RespuestaNodo.Guardado);
        }
        catch (ErrorFlowTwin e) { return RespuestaNodo.DesdeError(Tipo, e); }
    }
}
