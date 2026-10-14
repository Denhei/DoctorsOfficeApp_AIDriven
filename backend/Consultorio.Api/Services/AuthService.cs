using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<UsuarioInfoDto?> ObterUsuarioAtualAsync(Guid usuarioId);
}

public class AuthService(UserManager<Usuario> userManager, AppDbContext context, IJwtTokenGenerator jwtTokenGenerator) : IAuthService
{
    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var usuario = await userManager.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email || u.UserName == dto.Email);

        if (usuario == null || !await userManager.CheckPasswordAsync(usuario, dto.Senha))
            return null;

        var info = (await ObterUsuarioAtualAsync(usuario.Id))!;
        var (token, expiraEm) = jwtTokenGenerator.GerarToken(usuario, info.PacienteId, info.MedicoId);

        return new AuthResponseDto(token, expiraEm, info);
    }

    public Task<UsuarioInfoDto?> ObterUsuarioAtualAsync(Guid usuarioId)
        => userManager.Users.Where(u => u.Id == usuarioId).Select(Mapeamentos.UsuarioInfo(context)).FirstOrDefaultAsync();
}
