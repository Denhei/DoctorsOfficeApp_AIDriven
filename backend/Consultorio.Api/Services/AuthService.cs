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

public class AuthService : IAuthService
{
    private readonly UserManager<Usuario> _userManager;
    private readonly AppDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(UserManager<Usuario> userManager, AppDbContext context, IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var usuario = await _userManager.Users
            .Include(u => u.Instituicao)
            .FirstOrDefaultAsync(u => u.Email == dto.Email || u.UserName == dto.Email);

        if (usuario == null)
            return null;

        var senhaValida = await _userManager.CheckPasswordAsync(usuario, dto.Senha);
        if (!senhaValida)
            return null;

        Guid? pacienteId = null;
        Guid? medicoId = null;

        if (usuario.Perfil == Models.Enums.PerfilUsuario.Paciente)
        {
            var pac = await _context.Pacientes.FirstOrDefaultAsync(p => p.UsuarioId == usuario.Id);
            pacienteId = pac?.Id;
        }
        else if (usuario.Perfil == Models.Enums.PerfilUsuario.Medico)
        {
            var med = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == usuario.Id);
            medicoId = med?.Id;
        }

        var (token, expiraEm) = _jwtTokenGenerator.GerarToken(usuario, pacienteId, medicoId);

        var userInfo = new UsuarioInfoDto(
            usuario.Id,
            usuario.NomeCompleto,
            usuario.Email ?? string.Empty,
            usuario.Perfil,
            usuario.InstituicaoId,
            usuario.Instituicao?.Nome,
            pacienteId,
            medicoId
        );

        return new AuthResponseDto(token, expiraEm, userInfo);
    }

    public async Task<UsuarioInfoDto?> ObterUsuarioAtualAsync(Guid usuarioId)
    {
        var usuario = await _userManager.Users
            .Include(u => u.Instituicao)
            .FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario == null)
            return null;

        Guid? pacienteId = null;
        Guid? medicoId = null;

        if (usuario.Perfil == Models.Enums.PerfilUsuario.Paciente)
        {
            var pac = await _context.Pacientes.FirstOrDefaultAsync(p => p.UsuarioId == usuario.Id);
            pacienteId = pac?.Id;
        }
        else if (usuario.Perfil == Models.Enums.PerfilUsuario.Medico)
        {
            var med = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == usuario.Id);
            medicoId = med?.Id;
        }

        return new UsuarioInfoDto(
            usuario.Id,
            usuario.NomeCompleto,
            usuario.Email ?? string.Empty,
            usuario.Perfil,
            usuario.InstituicaoId,
            usuario.Instituicao?.Nome,
            pacienteId,
            medicoId
        );
    }
}
