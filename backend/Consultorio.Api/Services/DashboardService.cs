using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IDashboardService
{
    Task<DashboardOperacionalDto> ObterDashboardOperacionalAsync(Guid? instituicaoId);
}

public class DashboardService(AppDbContext context) : IDashboardService
{
    public async Task<DashboardOperacionalDto> ObterDashboardOperacionalAsync(Guid? instituicaoId)
    {
        var consultas = context.Consultas.AsQueryable();
        var coletas = context.ColetasIniciais.AsQueryable();
        var pesquisas = context.PesquisasSatisfacao.AsQueryable();

        if (instituicaoId.HasValue)
        {
            consultas = consultas.Where(c => c.Slot.InstituicaoId == instituicaoId.Value);
            coletas = coletas.Where(c => c.InstituicaoId == instituicaoId.Value);
            pesquisas = pesquisas.Where(p => p.Consulta.Slot.InstituicaoId == instituicaoId.Value);
        }

        var porStatus = await consultas
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Total);

        var totalColetasPendentes = await coletas.CountAsync(c => c.Status == StatusColeta.PendenteValidacao);

        var totalPesquisas = await pesquisas.CountAsync();
        var mediaSatisfacao = totalPesquisas > 0 ? await pesquisas.AverageAsync(p => p.Nota) : 0.0;

        return new DashboardOperacionalDto(
            porStatus.GetValueOrDefault(StatusConsulta.Agendada),
            porStatus.GetValueOrDefault(StatusConsulta.EmAtendimento),
            porStatus.GetValueOrDefault(StatusConsulta.Concluida),
            porStatus.GetValueOrDefault(StatusConsulta.Cancelada),
            totalColetasPendentes,
            totalPesquisas,
            Math.Round(mediaSatisfacao, 2)
        );
    }
}
