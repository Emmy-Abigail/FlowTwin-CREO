namespace FlowTwin.Gemelo.Contracts;

/// <summary>Lo que otro módulo necesita saber de una zona.</summary>
public sealed record ZonaInfo(int Id, string Nombre, int PisoId);

/// <summary>Un piso publicado y la sede a la que pertenece.</summary>
public sealed record PisoInfo(int Id, string Nombre, int SedeId);

/// <summary>Una zona tal como aparece en el recorrido de una atención. Tipo: como en ZonaSemaforo.</summary>
public sealed record ZonaRecorrido(int Id, string Nombre, string Tipo, int PisoId);

/// <summary>Una zona publicada con lo que hace falta para su semáforo. Tipo: INGRESO, ESPERA, TRIAJE, SERVICIO, TRANSITO o SALIDA. Aforo solo en Ingreso y Espera (ADR-27).</summary>
public sealed record ZonaSemaforo(int Id, string Nombre, string Tipo, short? Aforo);

/// <summary>Consultas de zonas para otros módulos (por ejemplo, Dispositivos al registrar un nodo, RN-19).</summary>
public interface IZonasConsultas
{
    /// <summary>El piso si existe y está publicado; null si no.</summary>
    Task<PisoInfo?> PisoAsync(int pisoId, CancellationToken ct = default);

    /// <summary>Las zonas publicadas del piso (las que están en borrador no se ven en el gemelo, RN-15).</summary>
    Task<IReadOnlyList<ZonaSemaforo>> DelPisoAsync(int pisoId, CancellationToken ct = default);

    /// <summary>La zona activa con ese Id, o null si no existe o está dada de baja.</summary>
    Task<ZonaInfo?> ZonaAsync(int zonaId, CancellationToken ct = default);

    Task<IReadOnlyList<ZonaInfo>> ZonasAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default);

    /// <summary>
    /// Nombre y tipo de esas zonas, INCLUIDAS las dadas de baja: el recorrido de una atención pasada
    /// debe seguir mostrando la zona por la que pasó, aunque hoy ya no exista.
    /// </summary>
    Task<IReadOnlyList<ZonaRecorrido>> ParaRecorridoAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default);
}

/// <summary>Un nodo tal como se dibuja en el plano (sección "La geometría que recibe el navegador").</summary>
public sealed record NodoEnPlano(string Codigo, decimal X, decimal Y, int ZonaId, string Estado);

/// <summary>
/// Inversión de dependencias: Gemelo necesita los nodos para la geometría, pero no puede depender
/// de Dispositivos (Dispositivos ya depende de Gemelo y se formaría un ciclo). Por eso Gemelo
/// declara aquí lo que necesita y Dispositivos lo implementa.
/// </summary>
public interface IUbicacionNodos
{
    /// <summary>Nodos con posición conocida de esas zonas, sin los dados de baja.</summary>
    Task<IReadOnlyList<NodoEnPlano>> DeZonasAsync(IReadOnlyCollection<int> zonaIds, CancellationToken ct = default);
}

/// <summary>Los parámetros del sistema que leen otros módulos (alertas, semáforo, cierre de jornada). ADR-27.</summary>
public sealed record ParametrosInfo(int EsperaAltaMin, int EsperaCriticaMin, int SinLecturasMin, int HoraCierre, int SemaforoAmbarPct);

public interface IParametrosSistema
{
    /// <summary>Los parámetros vigentes. Se guardan en memoria unos segundos: el gemelo los pide cada medio segundo.</summary>
    Task<ParametrosInfo> ObtenerAsync(CancellationToken ct = default);
}

/// <summary>
/// Inversión de dependencias, como IUbicacionNodos: Configuración (Gemelo) necesita saber si una zona tiene
/// nodos antes de darla de baja, pero no puede depender de Dispositivos. Gemelo lo declara; Dispositivos lo implementa.
/// </summary>
public interface INodosDeZonas
{
    /// <summary>Nodos que no están de baja en esa zona.</summary>
    Task<int> ActivosAsync(int zonaId, CancellationToken ct = default);
}
