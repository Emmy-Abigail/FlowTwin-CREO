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
/// Datos de DESARROLLO, solo con BaseDatos:SembrarDemo = true. Solo lo que existe físicamente: los
/// usuarios de prueba (contraseña demo123), las 15 tarjetas del piloto (todas Disponibles) y 3 nodos con
/// claves conocidas para el simulador. NUNCA historia inventada (atenciones, movimientos, lecturas):
/// todo lo que muestran las pantallas debe haber ocurrido de verdad. Los casos que necesita una
/// prueba los prepara la prueba con el flujo real. Producción solo tiene los datos de la migración.
/// Cada bloque se siembra solo si falta, así una base existente no se duplica.
/// </summary>
public static class SemillaDesarrollo
{
    public const string ClaveDemo = "demo123";

    /// <summary>Clave de desarrollo de un nodo: «nk_dev_N-01». Nunca se usa fuera de desarrollo.</summary>
    public static string ClaveNodoDemo(string codigo) => $"{ClaveNodo.Prefijo}dev_{codigo}";

    public static async Task SembrarAsync(IServiceProvider servicios, CancellationToken ct = default)
    {
        using var scope = servicios.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowTwinDbContext>();
        if (!await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Id != UsuarioSistema.Id, ct))
            await UsuariosYTarjetasAsync(scope.ServiceProvider, db, ct);
        if (!await db.Nodos.IgnoreQueryFilters().AnyAsync(ct))
            await NodosAsync(db, ct);
    }

    /// <summary>Los 3 nodos del prototipo, en sus zonas, con MAC de prueba (02:… = dirección local, no de fábrica).</summary>
    private static async Task NodosAsync(FlowTwinDbContext db, CancellationToken ct)
    {
        Nodo N(int n, int zonaId, decimal x, decimal y)
        {
            var nodo = new Nodo { Codigo = $"N-{n:D2}", Mac = $"02:00:00:00:00:{n:D2}", ZonaId = zonaId, PosX = x, PosY = y };
            nodo.ClaveApiHash = ClaveNodo.Hash(ClaveNodoDemo(nodo.Codigo));
            return nodo;
        }
        var nodos = new[] { N(1, 1, 36.0m, 11.2m), N(2, 3, 22.2m, 17.6m), N(3, 7, 5.9m, 17.7m) };
        db.Nodos.AddRange(nodos);
        await db.SaveChangesAsync(ct);
        db.EventosNodo.AddRange(nodos.Select(n => new EventoNodo { NodoId = n.Id, EstadoNodoId = n.EstadoNodoId, Detalle = "Registrado" }));
        await db.SaveChangesAsync(ct);
    }

    private static async Task UsuariosYTarjetasAsync(IServiceProvider sp, FlowTwinDbContext db, CancellationToken ct)
    {
        var hasher = sp.GetRequiredService<IPasswordHasher<Usuario>>();
        var reloj = sp.GetRequiredService<IReloj>();

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
            UuidBle = EmisorBle.Identificador(major: 1, minor: n),
        }).ToList();
        db.Tarjetas.AddRange(tarjetas);
        await db.SaveChangesAsync(ct);
        db.MovimientosTarjeta.AddRange(tarjetas.Select(t => new MovimientoTarjeta
        {
            TarjetaId = t.Id, EstadoNuevoId = EstadoTarjeta.Disponible, Observacion = "Registrada",
        }));
        await db.SaveChangesAsync(ct);
    }
}
