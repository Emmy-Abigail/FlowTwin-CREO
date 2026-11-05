using FlowTwin.Identidad.Application;
using FlowTwin.Identidad.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTwin.Identidad;

/// <summary>Registra todo lo que el módulo Identidad necesita. Lo llama Program.cs de la Api.</summary>
public static class ModuloIdentidad
{
    public static IServiceCollection AddModuloIdentidad(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OpcionesJwt>(config.GetSection("Jwt"));
        services.Configure<OpcionesContrasenas>(config.GetSection("Contrasenas"));
        // PBKDF2 con HMAC-SHA512, 100 000 iteraciones y sal aleatoria (sección Seguridad → Contraseñas)
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddScoped<AutenticacionServicio>();
        services.AddScoped<UsuariosServicio>();
        services.AddValidatorsFromAssemblyContaining<LoginValidador>(ServiceLifetime.Singleton);
        return services;
    }
}
