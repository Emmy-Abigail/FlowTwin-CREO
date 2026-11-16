using FlowTwin.Gemelo.Contracts;
using FlowTwin.Gemelo.Domain;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Gemelo.Application;

/// <summary>
/// Los parámetros vigentes, en memoria unos segundos: el gemelo los pide cada medio segundo por piso y no
/// tiene sentido ir a la base cada vez. Al guardar se olvidan, así el cambio se ve enseguida en esta instancia.
/// </summary>
public sealed class CacheParametros
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(15);
    private (ParametrosInfo Valor, DateTime Hasta)? guardado;
    private readonly Lock candado = new();

    public ParametrosInfo? Leer(DateTime ahora)
    {
        lock (candado) return guardado is { } g && g.Hasta > ahora ? g.Valor : null;
    }

    public void Guardar(ParametrosInfo valor, DateTime ahora)
    {
        lock (candado) guardado = (valor, ahora + Vigencia);
    }

    public void Olvidar()
    {
        lock (candado) guardado = null;
    }
}

/// <summary>Implementa IParametrosSistema leyendo la fila única de gem.ParametrosSistema.</summary>
public sealed class ParametrosServicio(IGemeloDb db, CacheParametros cache, IReloj reloj) : IParametrosSistema
{
    public async Task<ParametrosInfo> ObtenerAsync(CancellationToken ct = default)
    {
        if (cache.Leer(reloj.AhoraUtc) is { } enMemoria) return enMemoria;
        var p = await db.ParametrosSistema.AsNoTracking().SingleOrDefaultAsync(ct) ?? new ParametrosSistema();
        var info = Info(p);
        cache.Guardar(info, reloj.AhoraUtc);
        return info;
    }

    public static ParametrosInfo Info(ParametrosSistema p) =>
        new(p.EsperaAltaMin, p.EsperaCriticaMin, p.SinLecturasMin, p.HoraCierre, p.SemaforoAmbarPct);
}
