using System.Security.Claims;
using System.Text;
using FlowTwin.Identidad.Application;
using FlowTwin.Identidad.Domain;
using FlowTwin.SharedKernel;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FlowTwin.Api.Infraestructura;

/// <summary>
/// Firma el token de acceso: JWT HMAC-SHA256 de 15 minutos con sub, rol, pisos y debeCambiar
/// (sección Seguridad → Autenticación y sesión).
/// </summary>
public sealed class EmisorTokensJwt(IOptions<OpcionesJwt> opciones, IReloj reloj) : IEmisorTokens
{
    public (string Token, DateTime ExpiraEn) Emitir(Usuario usuario, IReadOnlyList<int> pisos, bool debeCambiar)
    {
        var o = opciones.Value;
        var expira = reloj.AhoraUtc.AddMinutes(o.MinutosAcceso);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Correo),
            new(JwtRegisteredClaimNames.Name, usuario.NombreCompleto),
            new(Claims.Rol, usuario.RolId.Codigo()),
            new(Claims.DebeCambiar, debeCambiar ? "true" : "false"),
        };
        claims.AddRange(pisos.Select(p => new Claim(Claims.Piso, p.ToString())));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Emisor,
            Audience = o.Emisor,
            Subject = new ClaimsIdentity(claims),
            NotBefore = reloj.AhoraUtc,
            Expires = expira,
            SigningCredentials = new SigningCredentials(Clave(o), SecurityAlgorithms.HmacSha256),
        });
        return (token, expira);
    }

    public static SymmetricSecurityKey Clave(OpcionesJwt o)
    {
        var bytes = Encoding.UTF8.GetBytes(o.Clave);
        if (bytes.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Clave debe tener al menos 32 bytes. Guárdala con: dotnet user-secrets set \"Jwt:Clave\" \"...\"");
        return new SymmetricSecurityKey(bytes);
    }
}
