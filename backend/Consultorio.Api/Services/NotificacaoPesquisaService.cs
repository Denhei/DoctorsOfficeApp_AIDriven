using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface INotificacaoPesquisaService
{
    Task<List<NotificacaoDto>> ListarNotificacoesPacienteAsync(UsuarioAtual paciente);
    Task<PesquisaSatisfacaoDto?> ObterPesquisaConsultaAsync(Guid consultaId, UsuarioAtual paciente);
    Task<PesquisaSatisfacaoDto> ResponderPesquisaAsync(Guid consultaId, ResponderPesquisaDto dto, UsuarioAtual paciente);
}

public class NotificacaoPesquisaService(AppDbContext context) : INotificacaoPesquisaService
{
    public async Task<List<NotificacaoDto>> ListarNotificacoesPacienteAsync(UsuarioAtual paciente)
    {
        var pacienteId = PacienteIdDe(paciente);

        return await context.NotificacoesSimuladas
            .Where(n => n.PacienteId == pacienteId)
            .OrderByDescending(n => n.CriadaEmUtc)
            .Select(Mapeamentos.Notificacao)
            .ToListAsync();
    }

    public async Task<PesquisaSatisfacaoDto?> ObterPesquisaConsultaAsync(Guid consultaId, UsuarioAtual paciente)
    {
        var pacienteId = PacienteIdDe(paciente);

        var consulta = await context.Consultas
            .Include(c => c.PesquisaSatisfacao)
            .FirstOrDefaultAsync(c => c.Id == consultaId && c.PacienteId == pacienteId);

        return consulta?.PesquisaSatisfacao?.ToDto();
    }

    public async Task<PesquisaSatisfacaoDto> ResponderPesquisaAsync(Guid consultaId, ResponderPesquisaDto dto, UsuarioAtual paciente)
    {
        var pacienteId = PacienteIdDe(paciente);

        var consulta = await context.Consultas
            .Include(c => c.PesquisaSatisfacao)
            .FirstOrDefaultAsync(c => c.Id == consultaId && c.PacienteId == pacienteId)
            ?? throw new RegraDeNegocioException("Consulta não encontrada.");

        if (consulta.Status != StatusConsulta.Concluida)
        {
            throw new RegraDeNegocioException("A pesquisa de satisfação só pode ser respondida após a conclusão da consulta.");
        }

        if (consulta.PesquisaSatisfacao != null)
        {
            throw new RegraDeNegocioException("Esta pesquisa de satisfação já foi respondida previamente.");
        }

        if (dto.Nota < 1 || dto.Nota > 5)
        {
            throw new RegraDeNegocioException("A nota de avaliação deve ser entre 1 e 5.");
        }

        var pesquisa = new PesquisaSatisfacao
        {
            ConsultaId = consultaId,
            PacienteId = pacienteId,
            Nota = dto.Nota,
            Percepcao = dto.Percepcao,
            Comentario = dto.Comentario,
            RespondidaEmUtc = DateTime.UtcNow
        };

        context.PesquisasSatisfacao.Add(pesquisa);
        await context.SaveChangesAsync();

        return pesquisa.ToDto();
    }

    private static Guid PacienteIdDe(UsuarioAtual usuario)
        => usuario.PacienteId ?? throw new AcessoNegadoException("Paciente não encontrado.");
}
