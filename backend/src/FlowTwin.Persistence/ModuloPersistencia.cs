using FlowTwin.Atenciones.Application;
using FlowTwin.Auditoria.Application;
using FlowTwin.Dispositivos.Application;
using FlowTwin.Gemelo.Application;
using FlowTwin.Identidad.Application;
using FlowTwin.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Persistence;

public static class ModuloPersistencia
{
    /// <summary>
    /// Registra el DbContext con su interceptor y le dice a cada módulo que su interfaz I…Db
    /// es este mismo contexto (una sola instancia por petición → una sola transacción).
    /// </summary>
    public static IServiceCollection AddPersistencia(this IServiceCollection services, IConfiguration config)
    {
        var cadena = config.GetConnectionString("FlowTwin");
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException(
                "Falta ConnectionStrings:FlowTwin. Guárdala con: dotnet user-secrets set \"ConnectionStrings:FlowTwin\" \"...\"");
        cadena = CadenaConexion.ConBase(cadena, config["BaseDatos:Nombre"]);

        services.AddScoped<RegistroInterceptor>();
        services.AddDbContext<FlowTwinDbContext>((sp, o) => o
            .UseSqlServer(cadena, sql => sql.MigrationsHistoryTable("__Migraciones", "dbo"))
            .AddInterceptors(sp.GetRequiredService<RegistroInterceptor>()));

        services.AddScoped<IIdentidadDb>(sp => sp.GetRequiredService<FlowTwinDbContext>());
        services.AddScoped<IGemeloDb>(sp => sp.GetRequiredService<FlowTwinDbContext>());
        services.AddScoped<IDispositivosDb>(sp => sp.GetRequiredService<FlowTwinDbContext>());
        services.AddScoped<IAtencionesDb>(sp => sp.GetRequiredService<FlowTwinDbContext>());
        services.AddScoped<IAuditoriaDb>(sp => sp.GetRequiredService<FlowTwinDbContext>());
        // Para los módulos sin tablas propias (Reportes) que solo guardan su fila de auditoría
        services.AddScoped<IUnidadDeTrabajo>(sp => sp.GetRequiredService<FlowTwinDbContext>());
        return services;
    }
}
