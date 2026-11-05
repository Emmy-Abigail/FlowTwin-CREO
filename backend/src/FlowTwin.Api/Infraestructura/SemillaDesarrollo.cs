using FlowTwin.Atenciones.Domain;
using FlowTwin.Dispositivos.Contracts;
using FlowTwin.Dispositivos.Domain;
using FlowTwin.Identidad.Domain;
using FlowTwin.Persistence;
using FlowTwin.SharedKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Datos de DEMOSTRACIÓN, solo en desarrollo (BaseDatos:SembrarDemo = true). Los mismos usuarios del
/// prototipo (contraseña demo123) y las 15 tarjetas del piloto, con dos casos para probar Admisión:
/// T-11 por desinfectar y T-14 sin devolver. Producción solo tiene los datos de la migración.
/// </summary>
public static class SemillaDesarrollo
{
    public const string ClaveDemo = "demo123";

    public static async Task SembrarAsync(IServiceProvider servicios, CancellationToken ct = default)
    {
        using var scope = servicios.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowTwinDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var zona = scope.ServiceProvider.GetRequiredService<ZonaHoraria>();
        var reloj = scope.ServiceProvider.GetRequiredService<IReloj>();

        if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Id != UsuarioSistema.Id, ct)) return;

        // ---- Usuarios del prototipo
        Usuario U(string correo, string nombres, string apellidos, Rol rol, bool debeCambiar = false, bool activo = true)
        {
            var u = new Usuario
            {
                Correo = correo, Nombres = nombres, Apellidos = apellidos, RolId = rol,
                DebeCambiarContrasena = debeCambiar, EstadoRegistro = activo,
                ContrasenaTemporalExpiraEn = debeCambiar ? reloj.AhoraUtc.AddDays(30) : null,
                Pisos = [new UsuarioPiso { PisoId = 1 }],
            };
            u.ContrasenaHash = hasher.HashPassword(u, ClaveDemo);
            return u;
        }
        db.Usuarios.AddRange(
            U("admin@creo.pe", "Ana", "Torres", Rol.Admin),
            U("operador@creo.pe", "Luis", "Rojas", Rol.Operador),
            U("gerencia@creo.pe", "Juan", "Morales", Rol.Gerencia),
            U("nuevo@creo.pe", "Carla", "Díaz", Rol.Operador, debeCambiar: true),
            U("psalas@creo.pe", "Pedro", "Salas", Rol.Operador, activo: false));

        // ---- 15 tarjetas BLE del piloto
        var tarjetas = Enumerable.Range(1, 15).Select(n => new Tarjeta
        {
            Codigo = $"T-{n:D2}", CodigoQr = $"CREO-T{n:D2}", SedeId = 1,
            UuidBle = $"f7826da6-4fa2-4e98-8024-bc5b71e0893e-{n:D4}",
        }).ToList();
        db.Tarjetas.AddRange(tarjetas);
        await db.SaveChangesAsync(ct);
        db.MovimientosTarjeta.AddRange(tarjetas.Select(t => new MovimientoTarjeta
        {
            TarjetaId = t.Id, EstadoNuevoId = EstadoTarjeta.Disponible, Observacion = "Registrada",
        }));
        await db.SaveChangesAsync(ct);

        // ---- Dos atenciones de ayer para los casos de error de Admisión
        var ayer = reloj.AhoraUtc.AddDays(-1);
        await CasoAsync(db, tarjetas[10], "A-0001", zona.FechaLocal(ayer), ayer, MotivoCierreAtencion.Devuelta, EstadoTarjeta.PorDesinfectar, ct);
        await CasoAsync(db, tarjetas[13], "A-0002", zona.FechaLocal(ayer), ayer.AddMinutes(20), MotivoCierreAtencion.SinLecturas, EstadoTarjeta.SinDevolver, ct);
    }

    private static async Task CasoAsync(FlowTwinDbContext db, Tarjeta tarjeta, string codigo, DateOnly fecha, DateTime inicio,
        MotivoCierreAtencion motivo, EstadoTarjeta estadoFinal, CancellationToken ct)
    {
        var atencion = Atencion.Abrir(codigo, fecha, tarjeta.SedeId, tarjeta.Id, inicio);
        db.Atenciones.Add(atencion);
        await db.SaveChangesAsync(ct);
        db.MovimientosTarjeta.Add(tarjeta.CambiarA(EstadoTarjeta.EnUso, atencion.Id));
        atencion.Cerrar(motivo, inicio.AddMinutes(40));
        db.MovimientosTarjeta.Add(tarjeta.CambiarA(estadoFinal, atencion.Id));
        await db.SaveChangesAsync(ct);
    }
}
