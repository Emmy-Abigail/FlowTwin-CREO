using FlowTwin.Identidad.Application;
using FlowTwin.Identidad.Domain;
using FlowTwin.Persistence;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Garantiza que el sistema tenga al menos un Administrador (RN-23). Si al arrancar no hay ninguno
/// activo, crea el de la sección "AdminInicial" con su contraseña temporal. Así la instalación en la
/// clínica empieza con un administrador que luego crea a los demás usuarios (F-04).
/// El correo y la contraseña NO están en el código ni en la migración: los define TI en el servidor
/// (variables de entorno) o tú en user-secrets.
/// </summary>
public static class AdminInicial
{
    public static async Task AsegurarAsync(IServiceProvider servicios, CancellationToken ct = default)
    {
        using var scope = servicios.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<FlowTwinDbContext>();

        // El filtro global ya excluye a los dados de baja: se busca un administrador ACTIVO.
        if (await db.Usuarios.AnyAsync(u => u.RolId == Rol.Admin && u.Id != UsuarioSistema.Id, ct)) return;

        var config = sp.GetRequiredService<IConfiguration>().GetSection("AdminInicial");
        var correo = config["Correo"]?.Trim().ToLowerInvariant();
        var clave = config["ClaveTemporal"];
        var nombres = config["Nombres"] ?? "Administrador";
        var apellidos = config["Apellidos"] ?? "CREO+";
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException(
                "No hay ningún Administrador activo. Define AdminInicial:Correo y AdminInicial:ClaveTemporal " +
                "(user-secrets en desarrollo, variables de entorno en el servidor) y vuelve a iniciar la API.");

        if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Correo == correo, ct))
            throw new InvalidOperationException(
                $"Ya existe un usuario con el correo {correo}. Usa otro correo en AdminInicial:Correo.");

        var incumplidos = PoliticaContrasena.Incumplidos(clave, correo, nombres, apellidos);
        if (incumplidos.Count > 0)
            throw new InvalidOperationException("AdminInicial:ClaveTemporal no cumple la política: " + string.Join(" ", incumplidos));

        var opciones = sp.GetRequiredService<IOptions<OpcionesContrasenas>>().Value;
        var admin = new Usuario { Correo = correo, Nombres = nombres, Apellidos = apellidos, RolId = Rol.Admin };
        admin.AsignarTemporal(
            sp.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(admin, clave),
            sp.GetRequiredService<IReloj>().AhoraUtc.AddHours(opciones.HorasTemporal));
        db.Usuarios.Add(admin);
        await db.SaveChangesAsync(ct);

        sp.GetRequiredService<IAuditoria>().Registrar(AccionAuditoria.Crear, "Usuario", admin.Id.ToString(),
            despues: new { admin.Correo, Rol = "ADMIN", origen = "AdminInicial" });
        await db.SaveChangesAsync(ct);

        sp.GetRequiredService<ILoggerFactory>().CreateLogger("FlowTwin.AdminInicial")
            .LogWarning("Se creó el Administrador inicial {Correo}. Cambia su contraseña temporal al ingresar.", correo);
    }
}
