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

/// <summary>Cada 30 s el ESP32 avisa que sigue vivo. RssiWifi y MemoriaLibre van al log para diagnosticar.</summary>
public sealed record LatidoRequest(string Mac, string? VersionFirmware, int? RssiWifi, int? MemoriaLibre);

public sealed class LatidoValidador : AbstractValidator<LatidoRequest>
{
    public LatidoValidador()
    {
        RuleFor(x => x.VersionFirmware).MaximumLength(20).WithMessage("La versión de firmware admite hasta 20 caracteres.");
        RuleFor(x => x.RssiWifi).InclusiveBetween(-127, 0).When(x => x.RssiWifi is not null);
        RuleFor(x => x.MemoriaLibre).GreaterThanOrEqualTo(0).When(x => x.MemoriaLibre is not null);
    }
}

/// <summary>Autenticación de nodos, latidos (F-15) y vigilancia de latidos (RN-20).</summary>
public sealed class IngestaNodosServicio(
    IDispositivosDb db, IReloj reloj, ZonaHoraria zona, IOptions<OpcionesNodos> opciones,
    IValidator<LatidoRequest> validador, ILogger<IngestaNodosServicio> log)
    : IAutenticacionNodos
{
    public async Task<NodoAutenticado> AutenticarAsync(string? clave, string? mac, CancellationToken ct = default)
    {
        var nodo = await NodoDeAsync(clave, mac, ct);
        return new NodoAutenticado(nodo.Id, nodo.Codigo, nodo.ZonaId, nodo.AjusteRssiDb);
    }

    /// <summary>POST /ingesta/latidos: registra el latido y, si estaba Sin señal, el nodo pasa a En línea.</summary>
    public async Task RegistrarLatidoAsync(string? clave, LatidoRequest req, CancellationToken ct)
    {
        // Primero quién es (401) y después si el formato es correcto (400): a quien no tiene clave
        // no se le da información sobre el formato que espera la API
        var nodo = await NodoDeAsync(clave, req.Mac, ct);
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

    /// <summary>
    /// Mismo mensaje para MAC desconocida, clave incorrecta o nodo de baja: a quien prueba claves
    /// no se le dice cuál de las tres falló.
    /// </summary>
    private async Task<Nodo> NodoDeAsync(string? clave, string? mac, CancellationToken ct)
    {
        var macNormal = mac is null ? null : Nodo.NormalizarMac(mac);
        var nodo = macNormal is null ? null : await db.Nodos.SingleOrDefaultAsync(n => n.Mac == macNormal, ct);
        if (nodo is null || string.IsNullOrEmpty(clave) || !nodo.ClaveCoincide(clave))
        {
            Metricas.Rechazos.Add(1, new KeyValuePair<string, object?>("motivo", "autenticacion"));
            log.LogWarning("Petición de nodo rechazada (MAC {Mac})", mac);
            throw new NoAutenticadoException("Nodo no autorizado.");
        }
        return nodo;
    }
}
