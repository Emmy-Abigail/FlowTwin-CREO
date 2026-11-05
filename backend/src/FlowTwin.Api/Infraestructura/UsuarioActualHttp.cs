using System.Diagnostics;
using FlowTwin.SharedKernel;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// IUsuarioActual a partir del token de la petición. Sin petición (procesos en segundo plano,
/// datos semilla) o sin sesión, es el usuario Sistema (ADR-19).
/// </summary>
public sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    private int? identificado;

    private HttpContext? Http => accesor.HttpContext;

    public bool EstaAutenticado => Http?.User.Identity?.IsAuthenticated == true;

    public int Id => identificado
        ?? (int.TryParse(Http?.User.FindFirst("sub")?.Value, out var id) ? id : UsuarioSistema.Id);

    public string? Rol => Http?.User.FindFirst(Claims.Rol)?.Value;

    public IReadOnlyList<int> Pisos =>
        [.. (Http?.User.FindAll(Claims.Piso) ?? []).Select(c => int.Parse(c.Value))];

    public string? Ip => Http?.Connection.RemoteIpAddress?.ToString();

    public string? TraceId => Activity.Current?.TraceId.ToString() ?? Http?.TraceIdentifier;

    public void IdentificarComo(int usuarioId) => identificado = usuarioId;
}
