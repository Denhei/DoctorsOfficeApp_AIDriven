using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IAdminService
{
    Task<List<UsuarioInfoDto>> ListarUsuariosAsync();
    Task<UsuarioInfoDto> CriarUsuarioAsync(CriarUsuarioAdminDto dto);
}

public class AdminService(AppDbContext context, UserManager<Usuario> userManager) : IAdminService
{
    public Task<List<UsuarioInfoDto>> ListarUsuariosAsync()
        => userManager.Users.Select(Mapeamentos.UsuarioInfo(context)).ToListAsync();

    public async Task<UsuarioInfoDto> CriarUsuarioAsync(CriarUsuarioAdminDto dto)
    {
        var usuario = await userManager.CriarUsuarioAsync(dto.Email, dto.NomeCompleto, dto.Senha, dto.Perfil, dto.InstituicaoId);

        if (dto.Perfil == PerfilUsuario.Medico)
        {
            if (!dto.InstituicaoId.HasValue)
                throw new RegraDeNegocioException("Médico deve ter uma instituição vinculada.");

            context.Medicos.Add(new Medico
            {
                UsuarioId = usuario.Id,
                InstituicaoId = dto.InstituicaoId.Value,
                Especialidade = dto.EspecialidadeMedico ?? "Clínica Geral",
                CrmFicticio = dto.CrmMedico ?? "CRM/SP 000000"
            });
            await context.SaveChangesAsync();
        }

        return await userManager.Users.Where(u => u.Id == usuario.Id).Select(Mapeamentos.UsuarioInfo(context)).FirstAsync();
    }
}
