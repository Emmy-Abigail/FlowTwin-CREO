using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowTwin.Dispositivos.Application;

/// <summary>Sección "Presencia" de appsettings: la ajusta el equipo técnico en la instalación.</summary>
public sealed class OpcionesNodos
{
    /// <summary>Sin latido por este tiempo, el nodo pasa a Sin señal (RN-20).</summary>
    public int LatidoMaximoSegundos { get; set; } = 120;
    /// <summary>Cada cuánto revisa el vigilante.</summary>
    public int RevisionLatidosSegundos { get; set; } = 30;
}

/// <summary>
/// Cada 30 s el ESP32 avisa que sigue vivo. RssiWifi y MemoriaLibre van al log para diagnosticar.
/// La MAC no viaja en el mensaje: es la del tema MQTT, que solo ese nodo puede usar (ADR-24).
/// </summary>
public sealed record LatidoRequest(string? VersionFirmware, int? RssiWifi, int? MemoriaLibre);

public sealed class LatidoValidador : AbstractValidator<LatidoRequest>
{
    public LatidoValidador()
    {
        RuleFor(x => x.VersionFirmware).MaximumLength(20).WithMessage("La versión de firmware admite hasta 20 caracteres.");
        RuleFor(x => x.RssiWifi).InclusiveBetween(-127, 0).When(x => x.RssiWifi is not null);
        RuleFor(x => x.MemoriaLibre).GreaterThanOrEqualTo(0).When(x => x.MemoriaLibre is not null);
    }
}

/// <summary>Nodos que envían datos, latidos (F-15) y vigilancia de latidos (RN-20).</summary>
public sealed class IngestaNodosServicio(
    IDispositivosDb db, IReloj reloj, ZonaHoraria zona, IOptions<OpcionesNodos> opciones,
    IValidator<LatidoRequest> validador, ILogger<IngestaNodosServicio> log)
    : INodosIngesta
{
    public async Task<NodoAutenticado?> ActivoPorMacAsync(string mac, CancellationToken ct = default)
    {
        var nodo = await NodoActivoAsync(mac, ct);
        return nodo is null ? null : new NodoAutenticado(nodo.Id, nodo.Codigo, nodo.ZonaId, nodo.AjusteRssiDb);
    }

    /// <summary>Latido por MQTT: registra la hora y, si estaba Sin señal, el nodo pasa a En línea.</summary>
    public async Task RegistrarLatidoAsync(string mac, LatidoRequest req, CancellationToken ct)
    {
        var nodo = await NodoActivoAsync(mac, ct) ?? throw Rechazado(mac);
        await validador.ValidarAsync(req, ct);
        nodo.UltimoLatidoEn = reloj.AhoraUtc;
        if (!string.IsNullOrWhiteSpace(req.VersionFirmware)) nodo.VersionFirmware = req.VersionFirmware.Trim();
        var evento = nodo.CambiarA(EstadoNodo.EnLinea, "Latido recibido");
        if (evento is not null)
        {
            db.EventosNodo.Add(evento);
            log.LogInformation("Nodo {Nodo} en línea (firmware {Firmware})", nodo.Codigo, nodo.VersionFirmware);
        }
        await db.GuardarAsync(ct);
        Metricas.Latidos.Add(1);
        log.LogDebug("Latido de {Nodo}: Wi-Fi {RssiWifi} dBm, memoria libre {Memoria} B", nodo.Codigo, req.RssiWifi, req.MemoriaLibre);
    }

    /// <summary>
    /// RN-20: pasa a Sin señal los nodos En línea sin latido en LatidoMaximoSegundos.
    /// Devuelve cuántos cambió. La alerta NODO_SIN_SENAL se agrega cuando exista ope.Alerta.
    /// </summary>
    public async Task<int> RevisarLatidosAsync(CancellationToken ct)
    {
        var limite = reloj.AhoraUtc.AddSeconds(-opciones.Value.LatidoMaximoSegundos);
        var callados = await db.Nodos
            .Where(n => n.EstadoNodoId == EstadoNodo.EnLinea && (n.UltimoLatidoEn == null || n.UltimoLatidoEn < limite))
            .ToListAsync(ct);
        foreach (var nodo in callados)
        {
            var desde = nodo.UltimoLatidoEn is { } u ? zona.ALocal(u).ToString("HH:mm") : "—";
            db.EventosNodo.Add(nodo.CambiarA(EstadoNodo.SinSenal, $"Sin latido desde {desde}")!);
            log.LogWarning("Nodo {Nodo} sin señal: último latido a las {Desde}", nodo.Codigo, desde);
        }
        if (callados.Count > 0) await db.GuardarAsync(ct);
        return callados.Count;
    }

    /// <summary>El nodo registrado con esa MAC, si no está de baja.</summary>
    private async Task<Nodo?> NodoActivoAsync(string mac, CancellationToken ct)
    {
        var macNormal = Nodo.NormalizarMac(mac);
        if (macNormal is null) return null;
        return await db.Nodos.SingleOrDefaultAsync(n => n.Mac == macNormal && n.EstadoNodoId != EstadoNodo.Baja, ct);
    }

    /// <summary>
    /// Con el broker no debería pasar (al dar de baja se borra la cuenta), pero si pasa se rechaza y queda en el
    /// log: puede ser una cuenta del broker que quedó sin borrar.
    /// </summary>
    private NoAutenticadoException Rechazado(string mac)
    {
        Metricas.Rechazos.Add(1, new KeyValuePair<string, object?>("motivo", "nodo_no_activo"));
        log.LogWarning("Mensaje de un nodo no registrado o de baja (MAC {Mac})", mac);
        return new NoAutenticadoException("Nodo no autorizado.");
    }
}
