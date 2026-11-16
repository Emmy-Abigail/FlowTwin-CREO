using System.Diagnostics;
using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FlowTwin.Atenciones.Application;

/// <summary>Sección "Ingesta" de appsettings.</summary>
public sealed class OpcionesIngesta
{
    /// <summary>Tope de lecturas por lote: protege al servidor de un lote gigante por error o ataque.</summary>
    public int MaxLecturasPorLote { get; set; } = 200;
    /// <summary>El nodo guarda hasta 10 min de lotes sin Wi-Fi; una lectura más vieja se rechaza.</summary>
    public int EdadMaximaMs { get; set; } = 10 * 60 * 1000;
    /// <summary>DD-11: las lecturas crudas se borran a los 7 días.</summary>
    public int RetencionLecturasDias { get; set; } = 7;
    /// <summary>Hora local de la purga diaria (03:00, fuera de la jornada).</summary>
    public int HoraPurga { get; set; } = 3;
}

/// <summary>
/// Un lote cada 2 s con lo que el nodo vio (contrato de integración, sección 4). La MAC no viaja en el
/// mensaje: es la del tema MQTT, que solo ese nodo puede usar (ADR-24).
/// </summary>
public sealed record LoteLecturasRequest(long Secuencia, List<LecturaRequest> Lecturas);

/// <summary>EdadMs: hace cuántos ms el nodo vio al emisor por última vez.</summary>
public sealed record LecturaRequest(string UuidBle, int Rssi, int EdadMs);

/// <summary>Recibidas: lecturas guardadas. Duplicado: el lote ya se había recibido y no se guardó otra vez.</summary>
public sealed record LoteRecibido(int Recibidas, bool Duplicado);

public sealed class LoteLecturasValidador : AbstractValidator<LoteLecturasRequest>
{
    public LoteLecturasValidador(IOptions<OpcionesIngesta> opciones)
    {
        var o = opciones.Value;
        RuleFor(x => x.Secuencia).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Lecturas).NotNull()
            .Must(l => l.Count <= o.MaxLecturasPorLote).WithMessage($"Un lote admite hasta {o.MaxLecturasPorLote} lecturas.");
        RuleForEach(x => x.Lecturas).ChildRules(l =>
        {
            l.RuleFor(x => x.UuidBle).NotEmpty().MaximumLength(64);
            l.RuleFor(x => x.Rssi).InclusiveBetween(-127, 0).WithMessage("El RSSI va de -127 a 0 dBm.");
            l.RuleFor(x => x.EdadMs).InclusiveBetween(0, o.EdadMaximaMs)
                .WithMessage($"edadMs va de 0 a {o.EdadMaximaMs} (10 minutos de búfer en el nodo).");
        });
    }
}

/// <summary>
/// Lecturas por MQTT (F-07): comprueba que el nodo esté activo, valida el lote, descarta reenvíos y guarda
/// las lecturas crudas. Debe tardar milisegundos: no hace cálculos de presencia aquí (los hace el motor).
/// </summary>
public sealed class IngestaServicio(
    IAtencionesDb db,
    INodosIngesta nodos,
    RegistroSecuencias secuencias,
    IValidator<LoteLecturasRequest> validador,
    IReloj reloj)
{
    public async Task<LoteRecibido> RecibirLoteAsync(string mac, LoteLecturasRequest req, CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        // Primero si el nodo está activo (RECHAZADO) y después si el formato es correcto (INVALIDO)
        var nodo = await nodos.ActivoPorMacAsync(mac, ct) ?? throw new NoAutenticadoException("Nodo no autorizado.");
        await validador.ValidarAsync(req, ct);

        if (!secuencias.Registrar(nodo.Id, req.Secuencia))
        {
            Metricas.LotesDuplicados.Add(1);
            return new LoteRecibido(0, Duplicado: true);
        }
        try
        {
            var recibidaEn = reloj.AhoraUtc;
            db.LecturasBle.AddRange(req.Lecturas.Select(l => LecturaBle.Desde(nodo.Id, l.UuidBle, l.Rssi, l.EdadMs, recibidaEn)));
            await db.GuardarAsync(ct);
        }
        catch
        {
            secuencias.Olvidar(nodo.Id, req.Secuencia);   // que el reintento del nodo sí se guarde
            throw;
        }

        Metricas.LotesRecibidos.Add(1);
        Metricas.LecturasRecibidas.Add(req.Lecturas.Count);
        Metricas.DuracionLote.Record(cronometro.Elapsed.TotalMilliseconds);
        return new LoteRecibido(req.Lecturas.Count, Duplicado: false);
    }
}
