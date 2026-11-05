using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Gemelo.Contracts;
using FlowTwin.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Atenciones.Application;

public interface IAtencionesDb : IUnidadDeTrabajo
{
    DbSet<Atencion> Atenciones { get; }
}

/// <summary>Modo del escaneo. AUTO deduce entrega o devolución por el estado de la tarjeta.</summary>
public sealed record EscaneoRequest(string CodigoQr, string? Modo = "AUTO");

public sealed record TarjetaEstadoDto(int Id, string Codigo, string Estado, string EstadoNombre);

/// <summary>Espera y consulta se calculan con los eventos de zona, que llegan con el motor de presencia (Avance 2).</summary>
public sealed record AtencionResumenDto(
    long Id, string Codigo, DateTime InicioEn, DateTime? FinEn, int? EsperaMin, int? ConsultaMin, int? TotalMin);

/// <summary>Operacion: ENTREGA, DEVOLUCION o RECUPERADA.</summary>
public sealed record EscaneoResultado(string Operacion, TarjetaEstadoDto Tarjeta, AtencionResumenDto? Atencion, DateTime? DeshacerHasta);

public sealed record TarjetaPendienteDto(int TarjetaId, string Codigo, DateTime? Desde, string? AtencionCodigo);
public sealed record OperacionDto(DateTime Fecha, string Tipo, string Tarjeta, string? Atencion, int? TotalMin);
public sealed record ResumenAdmisionDto(
    Dictionary<string, int> Contadores,
    List<TarjetaPendienteDto> PorDesinfectar,
    List<TarjetaPendienteDto> SinDevolver,
    List<OperacionDto> UltimasOperaciones);

public sealed class EscaneoValidador : AbstractValidator<EscaneoRequest>
{
    public EscaneoValidador()
    {
        RuleFor(x => x.CodigoQr).NotEmpty().WithMessage("Escanea o escribe el código de la tarjeta.").MaximumLength(64);
        RuleFor(x => x.Modo).Must(m => m is null or "AUTO" or "ENTREGA" or "DEVOLUCION")
            .WithMessage("El modo debe ser AUTO, ENTREGA o DEVOLUCION.");
    }
}

/// <summary>
/// Pantalla Admisión (P3): flujos F-06 (entregar), F-08 (devolver), deshacer (RN-04) y el resumen.
/// La tarjeta la maneja Dispositivos a través de su contrato; esta clase solo decide qué toca hacer.
/// </summary>
public sealed class AdmisionServicio(
    IAtencionesDb db,
    ITarjetasAdmision tarjetas,
    ISedesConsultas sedes,
    IAuditoria auditoria,
    IUsuarioActual usuario,
    IReloj reloj,
    ZonaHoraria zona,
    IValidator<EscaneoRequest> validador)
{
    public async Task<EscaneoResultado> EscanearAsync(EscaneoRequest req, CancellationToken ct)
    {
        await validador.ValidarAsync(req, ct);
        var modo = req.Modo ?? "AUTO";
        var tarjeta = await tarjetas.BuscarAsync(req.CodigoQr, ct)
            ?? throw new NoEncontradoException(
                $"Tarjeta {req.CodigoQr.Trim().ToUpperInvariant()} no registrada. Pide al Administrador que la registre.",
                "TARJETA_NO_REGISTRADA");

        var alcance = await sedes.SedesDelUsuarioAsync(ct);
        if (alcance is not null && !alcance.Contains(tarjeta.SedeId))
            throw new ReglaNegocioException("RN03_TARJETA_DE_OTRA_SEDE", $"La tarjeta {tarjeta.Codigo} pertenece a otra sede.");

        return tarjeta.Estado switch
        {
            EstadoTarjeta.Baja => throw new ReglaNegocioException("RN_TARJETA_DE_BAJA",
                $"La tarjeta {tarjeta.Codigo} está dada de baja. Usa otra tarjeta disponible."),
            EstadoTarjeta.PorDesinfectar => throw new ReglaNegocioException("RN03_POR_DESINFECTAR",
                "Esta tarjeta aún no está desinfectada.",
                new { tarjetaId = tarjeta.Id, tarjeta = tarjeta.Codigo, desde = tarjeta.Desde }),
            EstadoTarjeta.SinDevolver or EstadoTarjeta.Extraviada => await RecuperarAsync(tarjeta, ct),
            EstadoTarjeta.Disponible when modo == "DEVOLUCION" => throw new ReglaNegocioException("RN_TARJETA_NO_EN_USO",
                $"La tarjeta {tarjeta.Codigo} está disponible: no hay nada que recibir."),
            EstadoTarjeta.Disponible => await EntregarAsync(tarjeta, ct),
            EstadoTarjeta.EnUso when modo == "ENTREGA" => throw new ReglaNegocioException("RN01_TARJETA_CON_ATENCION_ABIERTA",
                $"La tarjeta {tarjeta.Codigo} ya está en uso. Para entregarla, primero recíbela."),
            _ => await DevolverAsync(tarjeta, ct),
        };
    }

    /// <summary>F-06: nace la atención y la tarjeta pasa a En uso, en una sola transacción.</summary>
    private Task<EscaneoResultado> EntregarAsync(TarjetaInfo tarjeta, CancellationToken ct) =>
        db.EnTransaccionAsync(async ct =>
        {
            var ahora = reloj.AhoraUtc;
            var fecha = zona.FechaLocal(ahora);
            var correlativo = await db.Atenciones.CountAsync(a => a.SedeId == tarjeta.SedeId && a.FechaAtencion == fecha, ct) + 1;
            var codigo = Atencion.FormatearCodigo(await sedes.PrefijoAtencionAsync(tarjeta.SedeId, ct), correlativo);

            var atencion = Atencion.Abrir(codigo, fecha, tarjeta.SedeId, tarjeta.Id, ahora);
            db.Atenciones.Add(atencion);
            await db.GuardarAsync(ct);   // aquí SQL Server le asigna el Id

            var t = await tarjetas.CambiarEstadoAsync(tarjeta.Id, EstadoTarjeta.EnUso, atencion.Id, ct: ct);
            auditoria.Registrar(AccionAuditoria.Entregar, "Atencion", atencion.Codigo, despues: new { tarjeta = tarjeta.Codigo });
            await db.GuardarAsync(ct);
            return new EscaneoResultado("ENTREGA", Dto(t), Dto(atencion, ahora), ahora + Atencion.VentanaDeshacer);
        }, ct);

    /// <summary>F-08: se cierra la atención como DEVUELTA y la tarjeta pasa a Por desinfectar.</summary>
    private async Task<EscaneoResultado> DevolverAsync(TarjetaInfo tarjeta, CancellationToken ct)
    {
        var ahora = reloj.AhoraUtc;
        var atencion = await db.Atenciones.SingleOrDefaultAsync(a => a.TarjetaId == tarjeta.Id && a.FinEn == null, ct)
            ?? throw new ConflictoException($"La tarjeta {tarjeta.Codigo} está en uso pero no tiene una atención abierta.");
        atencion.Cerrar(MotivoCierreAtencion.Devuelta, ahora, usuario.Id);
        var t = await tarjetas.CambiarEstadoAsync(tarjeta.Id, EstadoTarjeta.PorDesinfectar, atencion.Id, ct: ct);
        auditoria.Registrar(AccionAuditoria.Recibir, "Atencion", atencion.Codigo,
            despues: new { tarjeta = tarjeta.Codigo, totalMin = atencion.TotalMinutos(ahora) });
        await db.GuardarAsync(ct);
        return new EscaneoResultado("DEVOLUCION", Dto(t), Dto(atencion, ahora), null);
    }

    /// <summary>F-10: una tarjeta Sin devolver o Extraviada aparece y se escanea → Por desinfectar.</summary>
    private async Task<EscaneoResultado> RecuperarAsync(TarjetaInfo tarjeta, CancellationToken ct)
    {
        var t = await tarjetas.CambiarEstadoAsync(tarjeta.Id, EstadoTarjeta.PorDesinfectar, observacion: "Recuperada en Admisión", ct: ct);
        auditoria.Registrar(AccionAuditoria.Recuperar, "Tarjeta", tarjeta.Codigo,
            new { estado = tarjeta.Estado.Codigo() }, new { estado = t.Estado.Codigo() });
        await db.GuardarAsync(ct);
        return new EscaneoResultado("RECUPERADA", Dto(t), null, null);
    }

    /// <summary>RN-04: deshacer la entrega dentro de 30 s. La atención queda ANULADA y la tarjeta Disponible.</summary>
    public async Task<TarjetaEstadoDto> DeshacerAsync(long atencionId, CancellationToken ct)
    {
        var alcance = await sedes.SedesDelUsuarioAsync(ct);
        var atencion = await db.Atenciones.SingleOrDefaultAsync(a => a.Id == atencionId, ct);
        if (atencion is null || (alcance is not null && !alcance.Contains(atencion.SedeId)))
            throw new NoEncontradoException("La atención no existe.");

        atencion.Anular(reloj.AhoraUtc);
        var t = await tarjetas.CambiarEstadoAsync(atencion.TarjetaId, EstadoTarjeta.Disponible, atencion.Id, "Entrega deshecha", ct);
        auditoria.Registrar(AccionAuditoria.Deshacer, "Atencion", atencion.Codigo, new { estado = "ABIERTA" }, new { estado = "ANULADA" });
        await db.GuardarAsync(ct);
        return Dto(t);
    }

    /// <summary>Panel derecho de Admisión: contadores, listas y últimas operaciones.</summary>
    public async Task<ResumenAdmisionDto> ResumenAsync(CancellationToken ct)
    {
        var alcance = await sedes.SedesDelUsuarioAsync(ct);
        var lista = await tarjetas.ListarAsync(alcance, ct);
        var contadores = Enum.GetValues<EstadoTarjeta>()
            .ToDictionary(e => e.Codigo(), e => lista.Count(t => t.Estado == e));

        var sinDevolverIds = lista.Where(t => t.Estado == EstadoTarjeta.SinDevolver).Select(t => t.Id).ToList();
        var ultimaAtencion = await db.Atenciones
            .Where(a => sinDevolverIds.Contains(a.TarjetaId))
            .GroupBy(a => a.TarjetaId)
            .Select(g => new { TarjetaId = g.Key, Codigo = g.OrderByDescending(a => a.InicioEn).Select(a => a.Codigo).First() })
            .ToDictionaryAsync(x => x.TarjetaId, x => x.Codigo, ct);

        var movimientos = await tarjetas.UltimosMovimientosAsync(alcance, 8, ct);
        var atencionIds = movimientos.Where(m => m.AtencionId is not null).Select(m => m.AtencionId!.Value).Distinct().ToList();
        var atenciones = await db.Atenciones.Where(a => atencionIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var ahora = reloj.AhoraUtc;

        return new ResumenAdmisionDto(
            contadores,
            [.. lista.Where(t => t.Estado == EstadoTarjeta.PorDesinfectar).Select(t => new TarjetaPendienteDto(t.Id, t.Codigo, t.Desde, null))],
            [.. lista.Where(t => t.Estado == EstadoTarjeta.SinDevolver)
                .Select(t => new TarjetaPendienteDto(t.Id, t.Codigo, t.Desde, ultimaAtencion.GetValueOrDefault(t.Id)))],
            [.. movimientos.Select(m =>
            {
                var a = m.AtencionId is long id ? atenciones.GetValueOrDefault(id) : null;
                var tipo = TipoOperacion(m.Anterior, m.Nuevo);
                return new OperacionDto(m.Fecha, tipo, m.TarjetaCodigo, a?.Codigo,
                    tipo == "RECIBIDA" && a is not null ? a.TotalMinutos(ahora) : null);
            })]);
    }

    /// <summary>Nombre de la operación según el cambio de estado de la tarjeta.</summary>
    private static string TipoOperacion(EstadoTarjeta? anterior, EstadoTarjeta nuevo) => (anterior, nuevo) switch
    {
        (EstadoTarjeta.Disponible, EstadoTarjeta.EnUso) => "ENTREGADA",
        (EstadoTarjeta.EnUso, EstadoTarjeta.PorDesinfectar) => "RECIBIDA",
        (EstadoTarjeta.EnUso, EstadoTarjeta.Disponible) => "ANULADA",
        (EstadoTarjeta.EnUso, EstadoTarjeta.SinDevolver) => "SIN_DEVOLVER",
        (EstadoTarjeta.PorDesinfectar, EstadoTarjeta.Disponible) => "DESINFECTADA",
        (EstadoTarjeta.SinDevolver, EstadoTarjeta.Extraviada) => "EXTRAVIADA",
        (_, EstadoTarjeta.PorDesinfectar) => "RECUPERADA",
        (_, EstadoTarjeta.Baja) => "BAJA",
        _ => "REGISTRADA",
    };

    private static TarjetaEstadoDto Dto(TarjetaInfo t) => new(t.Id, t.Codigo, t.Estado.Codigo(), t.Estado.Nombre());

    private static AtencionResumenDto Dto(Atencion a, DateTime ahora) =>
        new(a.Id, a.Codigo, a.InicioEn, a.FinEn, null, null, a.FinEn is null ? null : a.TotalMinutos(ahora));
}
