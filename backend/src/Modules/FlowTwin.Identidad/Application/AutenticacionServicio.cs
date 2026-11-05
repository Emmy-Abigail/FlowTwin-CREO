using System.Security.Cryptography;
using FlowTwin.Identidad.Domain;
using FlowTwin.SharedKernel;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowTwin.Identidad.Application;

public sealed record LoginRequest(string Correo, string Clave);
public sealed record CambiarContrasenaRequest(string Actual, string Nueva, string Confirmacion);

public sealed class LoginValidador : AbstractValidator<LoginRequest>
{
    public LoginValidador()
    {
        RuleFor(x => x.Correo).NotEmpty().WithMessage("Escribe tu correo.").EmailAddress().WithMessage("El correo no es válido.");
        RuleFor(x => x.Clave).NotEmpty().WithMessage("Escribe tu contraseña.");
    }
}

public sealed class CambiarContrasenaValidador : AbstractValidator<CambiarContrasenaRequest>
{
    public CambiarContrasenaValidador()
    {
        RuleFor(x => x.Actual).NotEmpty().WithMessage("Escribe tu contraseña actual o temporal.");
        RuleFor(x => x.Nueva).NotEmpty().WithMessage("Escribe la nueva contraseña.");
        RuleFor(x => x.Confirmacion).Equal(x => x.Nueva).WithMessage("Las contraseñas no coinciden.");
    }
}

/// <summary>
/// Flujos F-01 (inicio de sesión), F-02 (primer ingreso) y F-03 (renovación y cierre).
/// El token de acceso dura 15 min y vive en memoria del navegador; el de refresco va en una
/// cookie HttpOnly y en la base solo se guarda su hash (ADR-13).
/// </summary>
public sealed class AutenticacionServicio(
    IIdentidadDb db,
    IEmisorTokens emisor,
    IPasswordHasher<Usuario> hasher,
    IReloj reloj,
    IUsuarioActual usuarioActual,
    IAuditoria auditoria,
    IOptions<OpcionesJwt> jwt,
    IOptions<OpcionesContrasenas> contrasenas,
    IValidator<LoginRequest> validadorLogin,
    IValidator<CambiarContrasenaRequest> validadorCambio)
{
    private TimeSpan Inactividad => TimeSpan.FromMinutes(jwt.Value.MinutosInactividad);

    /// <summary>¿Debe cambiar su contraseña ahora? Solo si la regla F-02 está activa y su contraseña es temporal.</summary>
    private bool ExigeCambio(Usuario u) => contrasenas.Value.ExigirCambioPrimerIngreso && u.DebeCambiarContrasena;

    /// <summary>F-01. Siempre responde el mismo mensaje genérico, sin decir qué falló.</summary>
    public async Task<SesionIniciada> IniciarSesionAsync(LoginRequest req, CancellationToken ct)
    {
        await validadorLogin.ValidarAsync(req, ct);
        var ahora = reloj.AhoraUtc;
        var correo = req.Correo.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.Include(u => u.Pisos)
            .SingleOrDefaultAsync(u => u.Correo == correo && u.Id != UsuarioSistema.Id, ct)
            ?? throw new NoAutenticadoException();

        if (usuario.EstaBloqueado(ahora))
            throw new BloqueadoException($"Cuenta bloqueada por {contrasenas.Value.MinutosBloqueo} minutos por demasiados intentos.");

        usuarioActual.IdentificarComo(usuario.Id);
        if (hasher.VerifyHashedPassword(usuario, usuario.ContrasenaHash, req.Clave) == PasswordVerificationResult.Failed)
        {
            usuario.RegistrarIntentoFallido(ahora, TimeSpan.FromMinutes(contrasenas.Value.MinutosBloqueo));
            await db.GuardarAsync(ct);
            if (usuario.EstaBloqueado(ahora))
                throw new BloqueadoException($"Cuenta bloqueada por {contrasenas.Value.MinutosBloqueo} minutos por demasiados intentos.");
            throw new NoAutenticadoException();
        }

        if (ExigeCambio(usuario) && usuario.ContrasenaTemporalExpiraEn < ahora)
            throw new NoAutenticadoException("La contraseña temporal venció. Pide al Administrador que la restablezca.");

        usuario.RegistrarIngreso(ahora);
        var sesion = AbrirSesion(usuario);
        auditoria.Registrar(AccionAuditoria.IniciarSesion, "Usuario", usuario.Id.ToString());
        await db.GuardarAsync(ct);
        return sesion;
    }

    /// <summary>F-03. Rota el token de refresco: el anterior queda revocado.</summary>
    public async Task<SesionIniciada> RefrescarAsync(string? tokenRefresco, CancellationToken ct)
    {
        var anterior = await BuscarSesionVigenteAsync(tokenRefresco, ct) ?? throw new NoAutenticadoException("La sesión expiró.");
        var usuario = await db.Usuarios.Include(u => u.Pisos).SingleOrDefaultAsync(u => u.Id == anterior.UsuarioId, ct);
        if (usuario is null) throw new NoAutenticadoException("La sesión expiró.");

        usuarioActual.IdentificarComo(usuario.Id);
        anterior.Revocar(reloj.AhoraUtc);
        var sesion = AbrirSesion(usuario, anterior.ExpiraEn);
        await db.GuardarAsync(ct);
        return sesion;
    }

    public async Task CerrarSesionAsync(string? tokenRefresco, CancellationToken ct)
    {
        var sesion = await BuscarSesionVigenteAsync(tokenRefresco, ct);
        if (sesion is null) return;
        usuarioActual.IdentificarComo(sesion.UsuarioId);
        sesion.Revocar(reloj.AhoraUtc);
        auditoria.Registrar(AccionAuditoria.CerrarSesion, "Usuario", sesion.UsuarioId.ToString());
        await db.GuardarAsync(ct);
    }

    /// <summary>F-02. Cambia la contraseña, revoca las demás sesiones y devuelve un token sin debeCambiar.</summary>
    public async Task<SesionIniciada> CambiarContrasenaAsync(CambiarContrasenaRequest req, CancellationToken ct)
    {
        await validadorCambio.ValidarAsync(req, ct);
        var ahora = reloj.AhoraUtc;
        var usuario = await db.Usuarios.Include(u => u.Pisos).SingleAsync(u => u.Id == usuarioActual.Id, ct);

        if (hasher.VerifyHashedPassword(usuario, usuario.ContrasenaHash, req.Actual) == PasswordVerificationResult.Failed)
            throw new ReglaNegocioException("CONTRASENA_ACTUAL_INCORRECTA", "La contraseña actual no es correcta.");

        var incumplidos = PoliticaContrasena.Incumplidos(req.Nueva, usuario.Correo, usuario.Nombres, usuario.Apellidos);
        if (incumplidos.Count > 0)
            throw new ReglaNegocioException("POLITICA_CONTRASENA", string.Join(" ", incumplidos), incumplidos);

        var anteriores = await db.HistorialContrasenas.Where(h => h.UsuarioId == usuario.Id)
            .OrderByDescending(h => h.Id).Take(contrasenas.Value.HistorialNoRepetir - 1)
            .Select(h => h.ContrasenaHash).ToListAsync(ct);
        anteriores.Add(usuario.ContrasenaHash);
        if (anteriores.Any(h => hasher.VerifyHashedPassword(usuario, h, req.Nueva) != PasswordVerificationResult.Failed))
            throw new ReglaNegocioException("CONTRASENA_REPETIDA",
                $"No puedes repetir ninguna de tus {contrasenas.Value.HistorialNoRepetir} últimas contraseñas.");

        db.HistorialContrasenas.Add(new HistorialContrasena { UsuarioId = usuario.Id, ContrasenaHash = usuario.ContrasenaHash });
        var primerIngreso = usuario.DebeCambiarContrasena;
        usuario.CambiarContrasena(hasher.HashPassword(usuario, req.Nueva), ahora);

        await db.Sesiones.Where(s => s.UsuarioId == usuario.Id && s.RevocadaEn == null)
            .ForEachAsync(s => s.Revocar(ahora), ct);
        var sesion = AbrirSesion(usuario);
        auditoria.Registrar(AccionAuditoria.CambiarContrasena, "Usuario", usuario.Id.ToString(),
            despues: new { primerIngreso });
        await db.GuardarAsync(ct);
        return sesion;
    }

    public async Task<UsuarioDto> YoAsync(CancellationToken ct)
    {
        var usuario = await db.Usuarios.Include(u => u.Pisos).SingleOrDefaultAsync(u => u.Id == usuarioActual.Id, ct)
            ?? throw new NoAutenticadoException("La sesión expiró.");
        return UsuarioDto.De(usuario, ExigeCambio(usuario));
    }

    private SesionIniciada AbrirSesion(Usuario usuario, DateTime? expiraEn = null)
    {
        var ahora = reloj.AhoraUtc;
        var token = RandomNumberGenerator.GetBytes(32);
        var sesion = new SesionUsuario
        {
            UsuarioId = usuario.Id,
            TokenRefrescoHash = SHA256.HashData(token),
            UltimaActividadEn = ahora,
            ExpiraEn = expiraEn ?? ahora.AddHours(jwt.Value.HorasSesion),
            Ip = usuarioActual.Ip,
        };
        db.Sesiones.Add(sesion);
        var dto = UsuarioDto.De(usuario, ExigeCambio(usuario));
        var (acceso, accesoExpira) = emisor.Emitir(usuario, dto.Pisos, dto.DebeCambiarContrasena);
        return new SesionIniciada(acceso, accesoExpira, dto,
            Convert.ToBase64String(token).TrimEnd('=').Replace('+', '-').Replace('/', '_'), sesion.ExpiraEn);
    }

    private async Task<SesionUsuario?> BuscarSesionVigenteAsync(string? tokenRefresco, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenRefresco)) return null;
        byte[] bytes;
        try
        {
            var base64 = tokenRefresco.Replace('-', '+').Replace('_', '/');
            bytes = Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
        }
        catch (FormatException) { return null; }
        var hash = SHA256.HashData(bytes);
        var sesion = await db.Sesiones.SingleOrDefaultAsync(s => s.TokenRefrescoHash == hash, ct);
        return sesion is not null && sesion.EstaVigente(reloj.AhoraUtc, Inactividad) ? sesion : null;
    }
}
