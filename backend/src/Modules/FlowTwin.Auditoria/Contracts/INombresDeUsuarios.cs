namespace FlowTwin.Auditoria.Contracts;

/// <summary>
/// Cómo se llama quien hizo cada acción. Lo declara Auditoría y lo implementa Identidad (contrato invertido,
/// como INodosDeZonas): Auditoría no puede leer la tabla de usuarios de otro módulo (pruebas de arquitectura).
/// </summary>
public interface INombresDeUsuarios
{
    /// <summary>Nombre completo por id, incluidos los usuarios dados de baja; el usuario Sistema se llama «Sistema».</summary>
    Task<IReadOnlyDictionary<int, string>> NombresAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
}
