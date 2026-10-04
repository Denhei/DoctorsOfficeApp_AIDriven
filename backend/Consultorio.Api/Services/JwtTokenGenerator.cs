using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Consultorio.Api.Models.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Consultorio.Api.Services;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiraEmUtc) GerarToken(Usuario usuario, Guid? pacienteId, Guid? medicoId);
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiraEmUtc) GerarToken(Usuario usuario, Guid? pacienteId, Guid? medicoId)
    {
        var secretKey = _configuration["JWT_SECRET_KEY"] 
            ?? _configuration["Jwt:SecretKey"] 
            ?? "CHAVE_SECRETA_PADRAO_POC_ACADEMICA_CONSULTORIO_2026_MINIMO_32_BYTES!";

        var issuer = _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"] ?? "ConsultorioApi";
        var audience = _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"] ?? "ConsultorioApp";
        var expiracaoHoras = int.TryParse(_configuration["JWT_EXPIRATION_HOURS"], out var h) ? h : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NomeCompleto),
            new(ClaimTypes.Role, usuario.Perfil.ToString()),
            new("perfil", usuario.Perfil.ToString())
        };

        if (usuario.InstituicaoId.HasValue)
        {
            claims.Add(new Claim("instituicaoId", usuario.InstituicaoId.Value.ToString()));
        }

        if (pacienteId.HasValue)
        {
            claims.Add(new Claim("pacienteId", pacienteId.Value.ToString()));
        }

        if (medicoId.HasValue)
        {
            claims.Add(new Claim("medicoId", medicoId.Value.ToString()));
        }

        var expiraEm = DateTime.UtcNow.AddHours(expiracaoHoras);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiraEm,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiraEm);
    }
}
