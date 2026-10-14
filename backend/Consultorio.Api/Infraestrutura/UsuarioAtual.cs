using System.Security.Claims;
using Consultorio.Api.Models.Enums;

namespace Consultorio.Api.Infraestrutura;

/// <summary>
/// Quem está fazendo a requisição, lido uma única vez dos claims que <c>JwtTokenGenerator</c> emite.
/// Os serviços recebem este registro em vez de redescobrir Paciente/Medico/Instituição no banco.
/// </summary>
public sealed record UsuarioAtual(Guid Id, PerfilUsuario Perfil, Guid? InstituicaoId, Guid? PacienteId, Guid? MedicoId)
{
    public static UsuarioAtual De(ClaimsPrincipal principal)
    {
        var id = GuidOuNulo(principal, ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Token sem identificador de usuário.");

        var perfil = Enum.TryParse<PerfilUsuario>(principal.FindFirstValue(ClaimTypes.Role), out var p)
            ? p
            : throw new UnauthorizedAccessException("Token sem perfil.");

        return new UsuarioAtual(
            id,
            perfil,
            GuidOuNulo(principal, "instituicaoId"),
            GuidOuNulo(principal, "pacienteId"),
            GuidOuNulo(principal, "medicoId"));
    }

    private static Guid? GuidOuNulo(ClaimsPrincipal principal, string tipo)
        => Guid.TryParse(principal.FindFirstValue(tipo), out var g) ? g : null;
}
