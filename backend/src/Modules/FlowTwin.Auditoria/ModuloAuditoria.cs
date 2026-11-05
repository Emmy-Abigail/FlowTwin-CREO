using FlowTwin.Auditoria.Application;
using FlowTwin.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Auditoria;

/// <summary>La consulta de la bitácora (GET /auditoria, pantalla P11) llega en el Avance 3.</summary>
public static class ModuloAuditoria
{
    public static IServiceCollection AddModuloAuditoria(this IServiceCollection services)
    {
        services.AddScoped<IAuditoria, AuditoriaServicio>();
        return services;
    }
}
