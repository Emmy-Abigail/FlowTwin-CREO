using System.Text.Json;
using FlowTwin.Mensajeria.Contracts;
using FlowTwin.SharedKernel;

namespace FlowTwin.Atenciones.Application;

/// <summary>Lotes de lecturas que llegan por MQTT a {prefijo}/v1/nodos/{mac}/lecturas (F-07).</summary>
public sealed class ManejadorLecturas(IngestaServicio ingesta) : IManejadorNodo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Tipo => "lecturas";

    public async Task<RespuestaNodo> ManejarAsync(MensajeNodo mensaje, CancellationToken ct)
    {
        LoteLecturasRequest? lote;
        try { lote = JsonSerializer.Deserialize<LoteLecturasRequest>(mensaje.Contenido.Span, Json); }
        catch (JsonException) { lote = null; }
        if (lote?.Lecturas is null)
            return new RespuestaNodo(Tipo, RespuestaNodo.Invalido, lote?.Secuencia,
                new Dictionary<string, string[]> { ["json"] = ["El lote no es un JSON válido con «secuencia» y «lecturas»."] });
        try
        {
            var r = await ingesta.RecibirLoteAsync(mensaje.Mac, lote, ct);
            return new RespuestaNodo(Tipo, r.Duplicado ? RespuestaNodo.Duplicado : RespuestaNodo.Guardado, lote.Secuencia);
        }
        catch (ErrorFlowTwin e) { return RespuestaNodo.DesdeError(Tipo, e, lote.Secuencia); }
    }
}
