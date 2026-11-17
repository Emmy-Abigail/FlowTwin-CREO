using FlowTwin.Mensajeria.Application;
using FlowTwin.Mensajeria.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Mensajeria;

/// <summary>MQTT con los nodos (ADR-24): el broker es Mosquitto; aquí están el suscriptor y la administración de cuentas.</summary>
public static class ModuloMensajeria
{
    public static IServiceCollection AddModuloMensajeria(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<OpcionesMqtt>().Bind(config.GetSection("Mqtt"))
            .Validate(o => !o.Activo || !string.IsNullOrWhiteSpace(o.ClaveAdmin),
                "Falta Mqtt:ClaveAdmin (la del administrador de Mosquitto). Guárdala con: dotnet user-secrets set \"Mqtt:ClaveAdmin\" \"...\"")
            .Validate(o => { try { _ = new TemasMqtt(o.Prefijo); return true; } catch (ArgumentException) { return false; } },
                "Mqtt:Prefijo no es válido: minúsculas, números y guiones, empezando con letra.")
            .ValidateOnStart();
        services.AddSingleton<AdministracionBroker>();
        services.AddSingleton<ICuentasNodosBroker>(sp => sp.GetRequiredService<AdministracionBroker>());
        services.AddSingleton<ServicioIngestaMqtt>();
        services.AddHostedService(sp => sp.GetRequiredService<ServicioIngestaMqtt>());
        services.AddHealthChecks().AddCheck<SaludBroker>("broker");
        return services;
    }
}
