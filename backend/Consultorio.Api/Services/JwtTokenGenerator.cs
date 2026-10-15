using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Consultorio.Api.Dominio;
using Consultorio.Api.Infraestrutura;
using Consultorio.Api.Models.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Consultorio.Api.Services;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiraEmUtc) GerarToken(Usuario usuario, Guid? pacienteId, Guid? medicoId);
}

public class JwtTokenGenerator(IOptions<JwtOptions> opcoes) : IJwtTokenGenerator
{
    public (string Token, DateTime ExpiraEmUtc) GerarToken(Usuario usuario, Guid? pacienteId, Guid? medicoId)
    {
        var jwt = opcoes.Value;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(UsuarioAtual.ParaClaims(usuario, pacienteId, medicoId));

        var expiraEm = DateTime.UtcNow.AddHours(jwt.ExpirationHours);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiraEm,
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(descriptor)), expiraEm);
    }
}
