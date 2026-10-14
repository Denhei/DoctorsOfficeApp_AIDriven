using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IInstituicaoService
{
    Task<List<InstituicaoDto>> ListarInstituicoesAsync();
    Task<List<MedicoDto>> ListarMedicosAsync(Guid? instituicaoId);
}

public class InstituicaoService(AppDbContext context) : IInstituicaoService
{
    public Task<List<InstituicaoDto>> ListarInstituicoesAsync()
        => context.Instituicoes
            .Where(i => i.Ativa)
            .Select(Mapeamentos.Instituicao)
            .ToListAsync();

    public Task<List<MedicoDto>> ListarMedicosAsync(Guid? instituicaoId)
    {
        var query = context.Medicos.AsQueryable();

        if (instituicaoId.HasValue)
        {
            query = query.Where(m => m.InstituicaoId == instituicaoId.Value);
        }

        return query.Select(Mapeamentos.Medico).ToListAsync();
    }
}
