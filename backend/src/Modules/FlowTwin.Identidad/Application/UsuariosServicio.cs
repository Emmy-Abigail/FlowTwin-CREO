using FlowTwin.Identidad.Domain;
using FlowTwin.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowTwin.Identidad.Application;

public sealed record GuardarUsuarioRequest(string Nombres, string Apellidos, string Correo, string Rol, int[] Pisos);

public sealed record UsuarioAdminDto(
    int Id, string Nombres, string Apellidos, string Correo, string Rol, int[] Pisos,
    bool EstadoRegistro, bool DebeCambiarContrasena, DateTime? UltimoIngresoEn,
    DateTime FechaRegistro, DateTime? FechaActualizacion);

/// <summary>La contraseña temporal se devuelve una sola vez (F-04, F-05).</summary>
public sealed record UsuarioConTemporal(UsuarioAdminDto Usuario, string ContrasenaTemporal);

public sealed class GuardarUsuarioValidador : AbstractValidator<GuardarUsuarioRequest>
{
    public GuardarUsuarioValidador()
    {
        RuleFor(x => x.Nombres).NotEmpty().WithMessage("Escribe los nombres.").MaximumLength(80);
        RuleFor(x => x.Apellidos).NotEmpty().WithMessage("Escribe los apellidos.").MaximumLength(80);
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().WithMessage("El correo no es válido.").MaximumLength(120);
        RuleFor(x => x.Rol).Must(r => Enum.GetValues<Rol>().Any(v => v.Codigo() == r))
            .WithMessage("El rol debe ser ADMIN, OPERADOR o GERENCIA.");
    }
}

/// <summary>Flujos F-04 (crear usuario) y F-05 (restablecer y desactivar). Solo Administrador (RN-22).</summary>
public sealed class UsuariosServicio(
    IIdentidadDb db,
    IPasswordHasher<Usuario> hasher,
    IReloj reloj,
    IUsuarioActual usuarioActual,
    IAuditoria auditoria,
    IOptions<OpcionesContrasenas> opciones,
    IValidator<GuardarUsuarioRequest> validador)
{
    /// <summary>Lista para la pantalla Usuarios; con incluirInactivos el Admin ve también los dados de baja (RN-29).</summary>
    public async Task<List<UsuarioAdminDto>> ListarAsync(bool incluirInactivos, CancellationToken ct)
    {
        var consulta = incluirInactivos ? db.Usuarios.IgnoreQueryFilters() : db.Usuarios;
        var usuarios = await consulta.Include(u => u.Pisos).Where(u => u.Id != UsuarioSistema.Id)
            .OrderBy(u => u.Apellidos).ToListAsync(ct);
        return [.. usuarios.Select(Dto)];
    }

    public async Task<UsuarioConTemporal> CrearAsync(GuardarUsuarioRequest req, CancellationToken ct)
    {
        await validador.ValidarAsync(req, ct);
        var rol = RolDe(req.Rol);
        ValidarPisos(rol, req.Pisos);
        var correo = req.Correo.Trim().ToLowerInvariant();
        if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Correo == correo, ct))
            throw new ReglaNegocioException("CORREO_DUPLICADO", "Ya existe un usuario con ese correo.");

        var usuario = new Usuario
        {
            Correo = correo, Nombres = req.Nombres.Trim(), Apellidos = req.Apellidos.Trim(), RolId = rol,
            Pisos = [.. req.Pisos.Distinct().Select(p => new UsuarioPiso { PisoId = p })],
        };
        var temporal = AsignarTemporal(usuario);
        db.Usuarios.Add(usuario);
        await db.GuardarAsync(ct);   // primero se guarda para tener el Id en la auditoría
        auditoria.Registrar(AccionAuditoria.Crear, "Usuario", usuario.Id.ToString(), despues: Resumen(usuario));
        await db.GuardarAsync(ct);
        return new UsuarioConTemporal(Dto(usuario), temporal);
    }

    public async Task<UsuarioAdminDto> EditarAsync(int id, GuardarUsuarioRequest req, CancellationToken ct)
    {
        await validador.ValidarAsync(req, ct);
        var usuario = await BuscarActivoAsync(id, ct);
        var rol = RolDe(req.Rol);
        ValidarPisos(rol, req.Pisos);
        var correo = req.Correo.Trim().ToLowerInvariant();
        if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Correo == correo && u.Id != id, ct))
            throw new ReglaNegocioException("CORREO_DUPLICADO", "Ya existe un usuario con ese correo.");
        if (usuario.RolId == Rol.Admin && rol != Rol.Admin) await ExigirOtroAdminActivoAsync(id, ct);

        var antes = Resumen(usuario);
        usuario.Nombres = req.Nombres.Trim();
        usuario.Apellidos = req.Apellidos.Trim();
        usuario.Correo = correo;
        usuario.RolId = rol;
        SincronizarPisos(usuario, req.Pisos);
        auditoria.Registrar(AccionAuditoria.Editar, "Usuario", id.ToString(), antes, Resumen(usuario));
        await db.GuardarAsync(ct);
        return Dto(usuario);
    }

    /// <summary>F-05: nueva temporal, obliga a cambiarla y cierra sus sesiones.</summary>
    public async Task<UsuarioConTemporal> RestablecerAsync(int id, CancellationToken ct)
    {
        var usuario = await BuscarActivoAsync(id, ct);
        var temporal = AsignarTemporal(usuario);
        await RevocarSesionesAsync(id, ct);
        auditoria.Registrar(AccionAuditoria.RestablecerContrasena, "Usuario", id.ToString());
        await db.GuardarAsync(ct);
        return new UsuarioConTemporal(Dto(usuario), temporal);
    }

    /// <summary>F-05: baja lógica (RN-22, RN-23, RN-26).</summary>
    public async Task DesactivarAsync(int id, CancellationToken ct)
    {
        if (id == usuarioActual.Id)
            throw new ReglaNegocioException("RN23_AUTODESACTIVACION", "No puedes desactivarte a ti mismo.");
        var usuario = await BuscarActivoAsync(id, ct);
        if (usuario.RolId == Rol.Admin) await ExigirOtroAdminActivoAsync(id, ct);
        usuario.EstadoRegistro = false;
        await RevocarSesionesAsync(id, ct);
        auditoria.Registrar(AccionAuditoria.Desactivar, "Usuario", id.ToString(),
            new { EstadoRegistro = true }, new { EstadoRegistro = false });
        await db.GuardarAsync(ct);
    }

    public async Task ActivarAsync(int id, CancellationToken ct)
    {
        var usuario = await db.Usuarios.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Id == id && u.Id != UsuarioSistema.Id, ct)
            ?? throw new NoEncontradoException("El usuario no existe.");
        if (usuario.EstadoRegistro) return;
        usuario.EstadoRegistro = true;
        auditoria.Registrar(AccionAuditoria.Activar, "Usuario", id.ToString(),
            new { EstadoRegistro = false }, new { EstadoRegistro = true });
        await db.GuardarAsync(ct);
    }

    private string AsignarTemporal(Usuario usuario)
    {
        var temporal = PoliticaContrasena.GenerarTemporal();
        usuario.AsignarTemporal(hasher.HashPassword(usuario, temporal), reloj.AhoraUtc.AddHours(opciones.Value.HorasTemporal));
        return temporal;
    }

    /// <summary>
    /// RN-29: un usuario dado de baja no se edita; primero se reactiva. Se cargan también sus pisos
    /// dados de baja para reactivarlos en vez de duplicarlos.
    /// </summary>
    private async Task<Usuario> BuscarActivoAsync(int id, CancellationToken ct) =>
        await db.Usuarios.IgnoreQueryFilters().Include(u => u.Pisos)
            .SingleOrDefaultAsync(u => u.Id == id && u.Id != UsuarioSistema.Id && u.EstadoRegistro, ct)
        ?? throw new NoEncontradoException("El usuario no existe o está dado de baja.");

    private async Task ExigirOtroAdminActivoAsync(int id, CancellationToken ct)
    {
        if (!await db.Usuarios.AnyAsync(u => u.RolId == Rol.Admin && u.Id != id && u.Id != UsuarioSistema.Id, ct))
            throw new ReglaNegocioException("RN23_ULTIMO_ADMIN", "Debe quedar al menos un Administrador activo.");
    }

    private async Task RevocarSesionesAsync(int usuarioId, CancellationToken ct)
    {
        var ahora = reloj.AhoraUtc;
        await db.Sesiones.Where(s => s.UsuarioId == usuarioId && s.RevocadaEn == null).ForEachAsync(s => s.Revocar(ahora), ct);
    }

    /// <summary>Activa, reactiva o da de baja las filas de UsuarioPiso: nunca se borran (RN-28).</summary>
    private void SincronizarPisos(Usuario usuario, int[] pisos)
    {
        var deseados = pisos.Distinct().ToHashSet();
        foreach (var p in usuario.Pisos) p.EstadoRegistro = deseados.Contains(p.PisoId);
        foreach (var nuevo in deseados.Where(id => usuario.Pisos.All(p => p.PisoId != id)))
            usuario.Pisos.Add(new UsuarioPiso { UsuarioId = usuario.Id, PisoId = nuevo });
    }

    private static void ValidarPisos(Rol rol, int[] pisos)
    {
        if (rol != Rol.Admin && pisos.Length == 0)
            throw new ReglaNegocioException("RN24_PISO_REQUERIDO", "Operador y Gerencia necesitan al menos un piso asignado.");
    }

    private static Rol RolDe(string codigo) => Enum.GetValues<Rol>().Single(r => r.Codigo() == codigo);

    private static object Resumen(Usuario u) => new
    {
        u.Nombres, u.Apellidos, u.Correo, Rol = u.RolId.Codigo(),
        Pisos = u.Pisos.Where(p => p.EstadoRegistro).Select(p => p.PisoId).Order().ToArray(),
    };

    private static UsuarioAdminDto Dto(Usuario u) => new(
        u.Id, u.Nombres, u.Apellidos, u.Correo, u.RolId.Codigo(),
        [.. u.Pisos.Where(p => p.EstadoRegistro).Select(p => p.PisoId).Order()],
        u.EstadoRegistro, u.DebeCambiarContrasena, u.UltimoIngresoEn, u.FechaRegistro, u.FechaActualizacion);
}
