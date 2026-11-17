using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowTwin.Atenciones.Application;

/// <summary>Sección "Presencia" de appsettings: la ajusta el equipo técnico en la instalación, no el Administrador.</summary>
public sealed class OpcionesPresencia
{
    public int RssiMinimo { get; set; } = -80;
    public int VentanaSegundos { get; set; } = 10;
    public int HisteresisSegundos { get; set; } = 45;
    public int SinLecturaSalidaSegundos { get; set; } = 120;
    /// <summary>Cada cuánto el motor busca lecturas nuevas en ope.LecturaBle.</summary>
    public int IntervaloMs { get; set; } = 1000;
    /// <summary>false en las pruebas de integración: ahí se llama a ProcesarAsync a mano.</summary>
    public bool MotorActivo { get; set; } = true;

    public ParametrosPresencia Parametros() => new(RssiMinimo,
        TimeSpan.FromSeconds(VentanaSegundos), TimeSpan.FromSeconds(HisteresisSegundos), TimeSpan.FromSeconds(SinLecturaSalidaSegundos));
}

/// <summary>
/// Estado del motor que sobrevive entre vueltas (singleton): el motor con sus rastreos y hasta qué
/// lectura se procesó. No se guarda en la base: al arrancar, o si una vuelta falla, se reconstruye.
/// </summary>
public sealed class EstadoPresencia
{
    public SemaphoreSlim Candado { get; } = new(1, 1);
    public MotorPresencia? Motor { get; set; }
    public long UltimaLecturaId { get; set; }
    /// <summary>Última UltimaLecturaEn escrita por atención, para escribirla como mucho cada 30 s.</summary>
    public Dictionary<long, DateTime> UltimaLecturaGuardada { get; } = [];
}

/// <summary>
/// Una vuelta del motor (ADR-21: la base es la cola). Lee las lecturas nuevas de ope.LecturaBle, las
/// traduce (emisor → atención abierta, nodo → zona + ajuste), alimenta al motor puro y guarda sus
/// decisiones como filas de ope.EventoZona. Ninguna lectura se pierde: si el servidor se cae, siguen
/// en la tabla.
/// </summary>
public sealed class ProcesadorPresencia(
    IAtencionesDb db,
    IPresenciaDispositivos dispositivos,
    EstadoPresencia estado,
    IReloj reloj,
    IOptions<OpcionesPresencia> opciones,
    ILogger<ProcesadorPresencia> log)
{
    /// <summary>Lecturas por vuelta: ~22/s en el piloto, el tope solo importa al ponerse al día tras un corte.</summary>
    public const int LoteMaximo = 5_000;
    private static readonly TimeSpan CadaCuantoUltimaLectura = TimeSpan.FromSeconds(30);

    /// <summary>Procesa lo pendiente y devuelve cuántas decisiones guardó.</summary>
    public async Task<int> ProcesarAsync(CancellationToken ct)
    {
        // Se adquiere FUERA del try: si se cancela mientras espera, el finally no debe liberar lo que no tomó
        await estado.Candado.WaitAsync(ct);
        try
        {
            var ahora = reloj.AhoraUtc;
            if (estado.Motor is null) await ReconstruirAsync(ahora, ct);
            var motor = estado.Motor!;

            var lecturas = await db.LecturasBle.AsNoTracking()
                .Where(l => l.Id > estado.UltimaLecturaId).OrderBy(l => l.Id).Take(LoteMaximo).ToListAsync(ct);
            var abiertas = await db.Atenciones.AsNoTracking().Where(a => a.FinEn == null)
                .Select(a => new { a.Id, a.TarjetaId, a.InicioEn }).ToListAsync(ct);
            motor.Olvidar(abiertas.Select(a => a.Id).ToHashSet());

            var decisiones = new List<DecisionPresencia>();
            var vistas = new Dictionary<long, DateTime>();
            if (lecturas.Count > 0)
            {
                var uuids = await dispositivos.UuidsDeTarjetasAsync([.. abiertas.Select(a => a.TarjetaId)], ct);
                var porEmisor = abiertas.Where(a => uuids.ContainsKey(a.TarjetaId)).ToDictionary(a => uuids[a.TarjetaId]);
                var nodos = (await dispositivos.NodosAsync(ct)).ToDictionary(n => n.Id);

                foreach (var l in lecturas.OrderBy(l => l.LeidaEn))
                {
                    // RN-12: solo cuentan los emisores con una atención abierta (tarjeta En uso), y solo
                    // desde que empezó esa atención (la misma tarjeta pudo usarla otro paciente antes)
                    if (!porEmisor.TryGetValue(l.UuidBle, out var a) || l.LeidaEn < a.InicioEn) continue;
                    if (!nodos.TryGetValue(l.NodoId, out var nodo)) continue;
                    decisiones.AddRange(motor.Registrar(a.Id, nodo.ZonaId, l.Rssi + nodo.AjusteRssiDb, l.LeidaEn));
                    if (!vistas.TryGetValue(a.Id, out var v) || l.LeidaEn > v) vistas[a.Id] = l.LeidaEn;
                }
            }
            decisiones.AddRange(motor.Revisar(ahora));

            await AplicarAsync(decisiones, vistas, ct);

            if (lecturas.Count > 0)
            {
                estado.UltimaLecturaId = lecturas[^1].Id;
                // Retraso de presencia: desde que la lectura llegó al servidor hasta que el motor la procesó
                Metricas.RetrasoPresencia.Record((ahora - lecturas.Min(l => l.FechaRegistro)).TotalMilliseconds);
            }
            return decisiones.Count;
        }
        catch
        {
            // Las decisiones de esta vuelta pudieron quedar a medio guardar: se reconstruye desde la base
            estado.Motor = null;
            throw;
        }
        finally
        {
            estado.Candado.Release();
        }
    }

    /// <summary>
    /// ADR-08: dónde estaba cada paciente (eventos abiertos) y las histéresis a medio camino (se
    /// reproducen las lecturas de los últimos SinLecturaSalida segundos). Como el motor es determinista,
    /// reproducir las lecturas da el mismo estado que si nunca se hubiera reiniciado.
    /// </summary>
    private async Task ReconstruirAsync(DateTime ahora, CancellationToken ct)
    {
        var o = opciones.Value;
        var motor = new MotorPresencia(o.Parametros());
        var abiertos = await db.EventosZona.AsNoTracking()
            .Where(e => e.SalidaEn == null)
            .Join(db.Atenciones.Where(a => a.FinEn == null), e => e.AtencionId, a => a.Id,
                (e, a) => new { e.AtencionId, e.ZonaId, e.EntradaEn, a.UltimaLecturaEn })
            .ToListAsync(ct);
        foreach (var e in abiertos)
            motor.Restaurar(e.AtencionId, e.ZonaId, e.UltimaLecturaEn > e.EntradaEn ? e.UltimaLecturaEn.Value : e.EntradaEn);

        var desde = ahora.AddSeconds(-o.SinLecturaSalidaSegundos);
        estado.UltimaLecturaId = await db.LecturasBle.Where(l => l.LeidaEn < desde).MaxAsync(l => (long?)l.Id, ct) ?? 0;
        estado.UltimaLecturaGuardada.Clear();
        estado.Motor = motor;
        log.LogInformation("Motor de presencia reconstruido: {Abiertos} pacientes en zona, reproduce lecturas desde {Desde:HH:mm:ss} UTC",
            abiertos.Count, desde);
    }

    private async Task AplicarAsync(List<DecisionPresencia> decisiones, Dictionary<long, DateTime> vistas, CancellationToken ct)
    {
        if (decisiones.Count > 0)
        {
            var ids = decisiones.Select(d => d.AtencionId).Distinct().ToList();
            // Se vuelve a mirar qué atenciones siguen abiertas: el Operador pudo devolver la tarjeta
            // mientras el motor pensaba, y no se abre una estadía para una atención ya cerrada
            var siguenAbiertas = (await db.Atenciones.Where(a => ids.Contains(a.Id) && a.FinEn == null)
                .Select(a => a.Id).ToListAsync(ct)).ToHashSet();
            var abiertos = await db.EventosZona.Where(e => ids.Contains(e.AtencionId) && e.SalidaEn == null)
                .ToDictionaryAsync(e => e.AtencionId, ct);

            foreach (var d in decisiones.Where(d => siguenAbiertas.Contains(d.AtencionId)))
            {
                if (abiertos.Remove(d.AtencionId, out var actual)) actual.Cerrar(d.En);   // RN-11: nunca dos abiertos
                if (d is EntradaZona entrada)
                {
                    var nuevo = new EventoZona { AtencionId = entrada.AtencionId, ZonaId = entrada.ZonaId, EntradaEn = entrada.En };
                    db.EventosZona.Add(nuevo);
                    abiertos[entrada.AtencionId] = nuevo;
                    Metricas.EntradasZona.Add(1);
                }
            }
            await db.GuardarAsync(ct);
        }

        // UltimaLecturaEn la usa el cierre por 60 min sin lecturas (RN-07): con 30 s de precisión sobra,
        // y escribir cada segundo sobre la atención chocaría con el Operador al devolver la tarjeta
        foreach (var (id, vista) in vistas)
        {
            if (estado.UltimaLecturaGuardada.TryGetValue(id, out var guardada) && vista - guardada < CadaCuantoUltimaLectura) continue;
            await db.Atenciones.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.UltimaLecturaEn, vista), ct);
            estado.UltimaLecturaGuardada[id] = vista;
        }
    }
}

/// <summary>Trabajo en segundo plano "MotorPresencia": una vuelta cada IntervaloMs.</summary>
public sealed class MotorPresenciaEnSegundoPlano(IServiceScopeFactory scopes, IOptions<OpcionesPresencia> opciones,
    ILogger<MotorPresenciaEnSegundoPlano> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        if (!opciones.Value.MotorActivo)
        {
            log.LogWarning("Motor de presencia apagado (Presencia:MotorActivo = false)");
            return;
        }
        using var temporizador = new PeriodicTimer(TimeSpan.FromMilliseconds(opciones.Value.IntervaloMs));
        while (await temporizador.WaitForNextTickAsync(detener))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ProcesadorPresencia>().ProcesarAsync(detener);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falló una vuelta del motor de presencia; se reconstruye y se reintenta");
            }
        }
    }
}
