namespace FlowTwin.SharedKernel;

/// <summary>
/// Errores que el dominio lanza y la API traduce a ProblemDetails (sección "Traducción de errores").
/// El Codigo es estable (por ejemplo RN01_TARJETA_CON_ATENCION_ABIERTA): el frontend decide con él.
/// </summary>
public abstract class ErrorFlowTwin(string codigo, string mensaje, int estadoHttp, object? datos = null)
    : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
    public int EstadoHttp { get; } = estadoHttp;
    public object? Datos { get; } = datos;
}

/// <summary>400: una regla de negocio (RN-nn) no se cumple.</summary>
public sealed class ReglaNegocioException(string codigo, string mensaje, object? datos = null)
    : ErrorFlowTwin(codigo, mensaje, 400, datos);

/// <summary>404: lo pedido no existe.</summary>
public sealed class NoEncontradoException(string mensaje, string codigo = "NO_ENCONTRADO", object? datos = null)
    : ErrorFlowTwin(codigo, mensaje, 404, datos);

/// <summary>409: el estado cambió mientras el usuario miraba la pantalla.</summary>
public sealed class ConflictoException(string mensaje, object? datos = null)
    : ErrorFlowTwin("CONFLICTO", mensaje, 409, datos);

/// <summary>401: sin sesión, credenciales incorrectas o sesión vencida.</summary>
public sealed class NoAutenticadoException(string mensaje = "Correo o contraseña incorrectos.")
    : ErrorFlowTwin("NO_AUTENTICADO", mensaje, 401);

/// <summary>403: el rol o el piso no permiten la acción.</summary>
public sealed class SinPermisoException(string mensaje = "No tienes permiso para esta acción.")
    : ErrorFlowTwin("SIN_PERMISO", mensaje, 403);

/// <summary>429: cuenta bloqueada por intentos fallidos.</summary>
public sealed class BloqueadoException(string mensaje, object? datos = null)
    : ErrorFlowTwin("BLOQUEADO", mensaje, 429, datos);
