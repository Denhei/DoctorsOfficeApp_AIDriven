using System.Security.Claims;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;

namespace Consultorio.Api.Dominio;

/// <summary>
/// Quem está fazendo a requisição. Este tipo é o único que conhece os nomes dos claims:
/// <see cref="ParaClaims"/> os emite no login e <see cref="De"/> os lê a cada requisição.
/// Os serviços recebem este registro em vez de redescobrir Paciente/Medico/Instituição no banco.
/// </summary>
public sealed record UsuarioAtual(Guid Id, PerfilUsuario Perfil, Guid? InstituicaoId, Guid? PacienteId, Guid? MedicoId)
{
    private const string ClaimInstituicao = "instituicaoId";
    private const string ClaimPaciente = "pacienteId";
    private const string ClaimMedico = "medicoId";

    public static IEnumerable<Claim> ParaClaims(Usuario usuario, Guid? pacienteId, Guid? medicoId)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString());
        yield return new Claim(ClaimTypes.Name, usuario.NomeCompleto);
        yield return new Claim(ClaimTypes.Role, usuario.Perfil.ToString());
        if (usuario.InstituicaoId is { } instituicaoId) yield return new Claim(ClaimInstituicao, instituicaoId.ToString());
        if (pacienteId is { } pac) yield return new Claim(ClaimPaciente, pac.ToString());
        if (medicoId is { } med) yield return new Claim(ClaimMedico, med.ToString());
    }

    public static UsuarioAtual De(ClaimsPrincipal principal)
    {
        var id = GuidOuNulo(principal, ClaimTypes.NameIdentifier)
            ?? throw new AcessoNegadoException("Token sem identificador de usuário.");

        var perfil = Enum.TryParse<PerfilUsuario>(principal.FindFirstValue(ClaimTypes.Role), out var p)
            ? p
            : throw new AcessoNegadoException("Token sem perfil.");

        return new UsuarioAtual(
            id,
            perfil,
            GuidOuNulo(principal, ClaimInstituicao),
            GuidOuNulo(principal, ClaimPaciente),
            GuidOuNulo(principal, ClaimMedico));
    }

    private static Guid? GuidOuNulo(ClaimsPrincipal principal, string tipo)
        => Guid.TryParse(principal.FindFirstValue(tipo), out var g) ? g : null;
}
