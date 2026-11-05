using FluentValidation;

namespace FlowTwin.SharedKernel;

/// <summary>400 VALIDACION: los datos de entrada no tienen el formato correcto, con el detalle por campo.</summary>
public sealed class ValidacionException(IDictionary<string, string[]> errores)
    : ErrorFlowTwin("VALIDACION", "Los datos enviados no son válidos.", 400, errores)
{
    public IDictionary<string, string[]> Errores { get; } = errores;
}

public static class ValidacionExtensiones
{
    /// <summary>Valida la petición con FluentValidation y lanza ValidacionException si algo falla.</summary>
    public static async Task ValidarAsync<T>(this IValidator<T> validador, T instancia, CancellationToken ct = default)
    {
        var resultado = await validador.ValidateAsync(instancia, ct);
        if (resultado.IsValid) return;
        var errores = resultado.Errors
            .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        throw new ValidacionException(errores);
    }
}
