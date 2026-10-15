using Consultorio.Api.Models.Enums;

namespace Consultorio.Api.Dominio;

/// <summary>Nomes de papel usados em [Authorize(Roles = ...)], derivados do enum para não divergirem dele.</summary>
public static class Papeis
{
    public const string Paciente = nameof(PerfilUsuario.Paciente);
    public const string Atendente = nameof(PerfilUsuario.Atendente);
    public const string Enfermeiro = nameof(PerfilUsuario.Enfermeiro);
    public const string Medico = nameof(PerfilUsuario.Medico);
    public const string Admin = nameof(PerfilUsuario.Admin);

    public const string Equipe = $"{Atendente},{Admin},{Medico},{Enfermeiro}";
    public const string Recepcao = $"{Atendente},{Admin}";
    public const string Triagem = $"{Enfermeiro},{Admin}";
}
