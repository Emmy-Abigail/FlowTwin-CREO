namespace FlowTwin.Gemelo.Contracts;

/// <summary>
/// Lo único que otros módulos pueden pedirle a Gemelo sobre sedes y pisos
/// (regla de la arquitectura: solo se usa la carpeta Contracts de otro módulo).
/// </summary>
public interface ISedesConsultas
{
    /// <summary>Sedes a las que pertenecen esos pisos (para saber qué tarjetas puede entregar un Operador, RN-03).</summary>
    Task<IReadOnlyList<int>> SedesDePisosAsync(IEnumerable<int> pisoIds, CancellationToken ct = default);

    /// <summary>Sedes que puede operar el usuario actual: null = todas (Administrador).</summary>
    Task<IReadOnlyList<int>?> SedesDelUsuarioAsync(CancellationToken ct = default);

    /// <summary>Letra con la que empiezan los códigos de atención de la sede.</summary>
    Task<char> PrefijoAtencionAsync(int sedeId, CancellationToken ct = default);
}
