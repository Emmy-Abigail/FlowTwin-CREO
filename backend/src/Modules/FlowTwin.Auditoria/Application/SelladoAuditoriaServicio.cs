using FlowTwin.Auditoria.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowTwin.Auditoria.Application;

public sealed class OpcionesAuditoria
{
    /// <summary>Si el sellado corre solo en segundo plano (las pruebas lo apagan y lo llaman a mano).</summary>
    public bool SelladoActivo { get; set; } = true;
    /// <summary>Clave del sello en base64 (≥ 32 bytes). Va en user-secrets o en una variable de entorno, nunca en la base.</summary>
    public string? ClaveSello { get; set; }
    /// <summary>
    /// Segundos que debe tener una fila antes de sellarla: da tiempo a que terminen las transacciones en curso, para
    /// que la cadena siga el orden de los Id sin saltarse una fila que se confirma tarde.
    /// </summary>
    public int MargenSelloSegundos { get; set; } = 60;
}

/// <summary>Resultado de verificar la cadena de un período. Alterada = se encontró una fila cuyo sello no coincide.</summary>
public sealed record ResultadoIntegridad(
    bool Configurada, int Revisados, int SinSellar, bool Integra, long? PrimeraAlteracionId, string? Motivo, DateTime? UltimoSelloEn);

/// <summary>
/// Sella las filas nuevas de la bitácora en orden de Id y verifica la cadena. Es determinista: dos instancias de la
/// API calculan el mismo sello, y si ambas lo insertan, la clave primaria rechaza la segunda sin dañar nada.
/// </summary>
public sealed class SelladoAuditoriaServicio(IAuditoriaDb db, IOptions<OpcionesAuditoria> opciones, IReloj reloj, ILogger<SelladoAuditoriaServicio> log)
{
    public const int LoteMaximo = 500;
    private const int PaginaVerificacion = 2_000;

    private byte[]? Clave => SelladoAuditoria.LeerClave(opciones.Value.ClaveSello);
    public bool Configurado => Clave is not null;
    private DateTime Corte => reloj.AhoraUtc.AddSeconds(-opciones.Value.MargenSelloSegundos);

    private static DatosSello Datos(RegistroAuditoria a) => new(a.Id, a.FechaRegistro, a.UsuarioRegistroId, (int)a.AccionAuditoriaId,
        a.Entidad, a.EntidadId, a.ValorAntes, a.ValorDespues, a.Ip, a.TraceId);

    /// <summary>Sella un lote de filas pendientes. Devuelve cuántas selló.</summary>
    public async Task<int> SellarPendientesAsync(CancellationToken ct)
    {
        if (Clave is not { } clave) return 0;
        var ultimo = await db.Sellos.AsNoTracking().OrderByDescending(s => s.AuditoriaId)
            .Select(s => new { s.AuditoriaId, s.Hash }).FirstOrDefaultAsync(ct);
        var desde = ultimo?.AuditoriaId ?? 0;
        var candidatas = await db.Auditoria.AsNoTracking().Where(a => a.Id > desde).OrderBy(a => a.Id).Take(LoteMaximo).ToListAsync(ct);

        var corte = Corte;
        var anterior = ultimo?.Hash ?? SelladoAuditoria.Origen;
        var sellados = 0;
        foreach (var fila in candidatas)
        {
            // Prefijo contiguo: en cuanto una fila es demasiado reciente se detiene, para no sellar una posterior
            // y dejar atrás una anterior que aún no se confirmó
            if (fila.FechaRegistro > corte) break;
            anterior = SelladoAuditoria.Calcular(clave, anterior, Datos(fila));
            db.Sellos.Add(new RegistroSello { AuditoriaId = fila.Id, Hash = anterior });
            sellados++;
        }
        if (sellados == 0) return 0;
        try
        {
            await db.GuardarAsync(ct);
        }
        catch (DbUpdateException e)
        {
            // Otra instancia ya selló estas filas (mismo resultado, la clave primaria lo rechaza): no es un error
            log.LogInformation(e, "Los sellos de la bitácora ya estaban escritos por otra instancia");
            return 0;
        }
        return sellados;
    }

    /// <summary>
    /// Recorre las filas del período y recalcula su cadena. La primera fila del período se ancla al sello guardado
    /// de la anterior; el resto debe encadenar exacto. Se detiene en la primera alteración.
    /// </summary>
    public async Task<ResultadoIntegridad> VerificarAsync(DateTime desdeUtc, DateTime hastaUtc, CancellationToken ct)
    {
        var ultimoSello = await db.Sellos.AsNoTracking().OrderByDescending(s => s.AuditoriaId).Select(s => (DateTime?)s.FechaRegistro).FirstOrDefaultAsync(ct);
        if (Clave is not { } clave) return new ResultadoIntegridad(false, 0, 0, false, null, null, ultimoSello);

        var enPeriodo = db.Auditoria.AsNoTracking().Where(a => a.FechaRegistro >= desdeUtc && a.FechaRegistro < hastaUtc);
        var primera = await enPeriodo.OrderBy(a => a.Id).Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);
        var ultima = await enPeriodo.OrderByDescending(a => a.Id).Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);
        if (primera is null || ultima is null) return new ResultadoIntegridad(true, 0, 0, true, null, null, ultimoSello);

        // Filas viejas sin sello: o el sellado está detenido, o alguien insertó una fila por fuera de la aplicación.
        // Se da el margen más dos vueltas de la tarea, para no sospechar de una fila que simplemente aún no le tocó
        var espera = opciones.Value.MargenSelloSegundos + 2 * (int)SelladoAuditoriaJob.Intervalo.TotalSeconds;
        var corte = reloj.AhoraUtc.AddSeconds(-espera);
        var sinSellar = await enPeriodo.CountAsync(a => a.Id >= primera && a.Id <= ultima && a.FechaRegistro <= corte
            && !db.Sellos.Any(s => s.AuditoriaId == a.Id), ct);

        var anterior = await db.Sellos.AsNoTracking().Where(s => s.AuditoriaId < primera).OrderByDescending(s => s.AuditoriaId)
            .Select(s => s.Hash).FirstOrDefaultAsync(ct) ?? SelladoAuditoria.Origen;

        var revisados = 0;
        var cursor = primera.Value - 1;
        while (true)
        {
            var pagina = await db.Sellos.AsNoTracking().Where(s => s.AuditoriaId > cursor && s.AuditoriaId <= ultima)
                .OrderBy(s => s.AuditoriaId).Take(PaginaVerificacion)
                .Join(db.Auditoria, s => s.AuditoriaId, a => a.Id, (s, a) => new { s.Hash, Fila = a }).ToListAsync(ct);
            if (pagina.Count == 0) break;
            foreach (var p in pagina)
            {
                var esperado = SelladoAuditoria.Calcular(clave, anterior, Datos(p.Fila));
                if (!SelladoAuditoria.Coincide(esperado, p.Hash))
                    return new ResultadoIntegridad(true, revisados, sinSellar, false, p.Fila.Id,
                        "El sello no coincide: la fila fue modificada o falta una fila anterior.", ultimoSello);
                anterior = p.Hash;
                revisados++;
                cursor = p.Fila.Id;
            }
        }
        return new ResultadoIntegridad(true, revisados, sinSellar, sinSellar == 0, null,
            sinSellar == 0 ? null : $"{sinSellar} fila(s) con más de {espera} s sin sello: el sellado estuvo detenido o se insertaron por fuera de la aplicación.",
            ultimoSello);
    }
}

/// <summary>Sella cada 30 s. Sin clave configurada no hace nada y lo dice en el registro al arrancar.</summary>
public sealed class SelladoAuditoriaJob(IServiceScopeFactory scopes, IOptions<OpcionesAuditoria> opciones, ILogger<SelladoAuditoriaJob> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        if (!opciones.Value.SelladoActivo) return;
        if (SelladoAuditoria.LeerClave(opciones.Value.ClaveSello) is null)
        {
            log.LogWarning("La bitácora NO se está sellando: falta Auditoria:ClaveSello (base64, mínimo 32 bytes) en user-secrets o variable de entorno");
            return;
        }
        using var temporizador = new PeriodicTimer(Intervalo);
        while (await temporizador.WaitForNextTickAsync(detener))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var servicio = scope.ServiceProvider.GetRequiredService<SelladoAuditoriaServicio>();
                while (await servicio.SellarPendientesAsync(detener) == SelladoAuditoriaServicio.LoteMaximo) { }   // se pone al día por lotes
            }
            catch (Exception e) when (!detener.IsCancellationRequested)
            {
                log.LogError(e, "Falló el sellado de la bitácora; se reintenta en 30 s");
            }
        }
    }
}
