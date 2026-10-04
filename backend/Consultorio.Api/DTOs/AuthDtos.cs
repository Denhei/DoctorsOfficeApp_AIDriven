using Consultorio.Api.Models.Enums;

namespace Consultorio.Api.DTOs;

public record LoginDto(string Email, string Senha);

public record UsuarioInfoDto(
    Guid Id,
    string Nome,
    string Email,
    PerfilUsuario Perfil,
    Guid? InstituicaoId,
    string? InstituicaoNome,
    Guid? PacienteId,
    Guid? MedicoId
);

public record AuthResponseDto(
    string Token,
    DateTime ExpiraEmUtc,
    UsuarioInfoDto Usuario
);
