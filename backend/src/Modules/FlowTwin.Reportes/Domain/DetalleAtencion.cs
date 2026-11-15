using FlowTwin.Atenciones.Contracts;
using FlowTwin.Gemelo.Contracts;

namespace FlowTwin.Reportes.Domain;

/// <summary>
/// GET /atenciones/{id} (P6). Los minutos son nulos cuando no hay con qué calcularlos: la consulta, si
/// nunca entró a una zona Servicio; los tres, si la entrega se anuló (RN-04: no cuenta en las métricas).
/// </summary>
public sealed record AtencionDetalle(
    long Id, string Codigo, DateOnly Fecha, string Sede, string Tarjeta,
    DateTime InicioEn, DateTime? FinEn, string Estado, string? Motivo,
    int? EsperaMin, int? ConsultaMin, int? TotalMin, IReadOnlyList<EstadiaDetalle> Estadias);

/// <summary>Una estadía con el nombre y el tipo de su zona. SalidaEn nula: está ahí ahora.</summary>
public sealed record EstadiaDetalle(int ZonaId, string Zona, string Tipo, DateTime EntradaEn, DateTime? SalidaEn);

/// <summary>Arma el detalle de una atención. Es puro: se prueba sin base ni reloj del sistema.</summary>
public static class CalculoDetalle
{
    /// <summary>Tipo de una zona que ya no se encuentra: se muestra igual, sin inventarle un tipo.</summary>
    public const string TipoDesconocido = "DESCONOCIDO";

    public static AtencionDetalle Construir(
        AtencionDetalleInfo a, IReadOnlyList<ZonaRecorrido> zonas, string sede, string tarjeta, DateTime ahora)
    {
        var porId = zonas.ToDictionary(z => z.Id);
        var estadias = a.Estadias.Select(e => porId.TryGetValue(e.ZonaId, out var z)
            ? new EstadiaDetalle(e.ZonaId, z.Nombre, z.Tipo, e.EntradaEn, e.SalidaEn)
            : new EstadiaDetalle(e.ZonaId, $"Zona {e.ZonaId}", TipoDesconocido, e.EntradaEn, e.SalidaEn)).ToList();

        if (a.Motivo == EstadoAtencion.Anulada)
            return new(a.Id, a.Codigo, a.Fecha, sede, tarjeta, a.InicioEn, a.FinEn, EstadoAtencion.Anulada, a.Motivo,
                null, null, null, estadias);

        bool esServicio(int zonaId) => porId.GetValueOrDefault(zonaId)?.Tipo == ReglaTiempos.Servicio;
        var hasta = a.FinEn ?? ahora;
        var t = ReglaTiempos.Calcular(a.Estadias, a.InicioEn, hasta, esServicio);
        var estado = a.FinEn is not null
            ? EstadoAtencion.Finalizada
            : ReglaTiempos.EstadoAbierta(a.Estadias.LastOrDefault(e => e.SalidaEn is null), t.PrimeraConsulta, esServicio);

        return new(a.Id, a.Codigo, a.Fecha, sede, tarjeta, a.InicioEn, a.FinEn, estado, a.Motivo,
            EsperaMin: ReglaTiempos.Minutos(t.Espera),
            ConsultaMin: t.PrimeraConsulta is null ? null : ReglaTiempos.Minutos(t.Consulta),
            TotalMin: ReglaTiempos.Minutos(hasta - a.InicioEn),
            estadias);
    }
}
