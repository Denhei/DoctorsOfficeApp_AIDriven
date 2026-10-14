using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

// --- Notificações Simuladas & Pesquisa de Satisfação ---
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

// --- Dashboard Operacional ---
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

// --- Instituições & Médicos ---
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

// --- Pacientes ---
public interface IPacienteService
{
    Task<List<PacienteDto>> ListarPacientesAsync();
    Task<PacienteDto?> ObterPacientePorIdAsync(Guid id, UsuarioAtual solicitante);
    Task<PacienteDto> CriarPacienteAsync(CriarPacienteDto dto);
    Task<PacienteDto> AtualizarPacienteAsync(Guid id, AtualizarPacienteDto dto, UsuarioAtual solicitante);
}

public class PacienteService(AppDbContext context, UserManager<Usuario> userManager) : IPacienteService
{
    public Task<List<PacienteDto>> ListarPacientesAsync()
        => context.Pacientes.Select(Mapeamentos.Paciente).ToListAsync();

    public async Task<PacienteDto?> ObterPacientePorIdAsync(Guid id, UsuarioAtual solicitante)
    {
        var paciente = await context.Pacientes.FindAsync(id);
        if (paciente == null) return null;

        if (solicitante.Perfil == PerfilUsuario.Paciente && paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Acesso negado aos dados de outro paciente.");
        }

        return paciente.ToDto();
    }

    public async Task<PacienteDto> CriarPacienteAsync(CriarPacienteDto dto)
    {
        var usuario = await userManager.CriarUsuarioAsync(dto.EmailFicticio, dto.Nome, dto.Senha, PerfilUsuario.Paciente);

        var paciente = new Paciente
        {
            UsuarioId = usuario.Id,
            Nome = dto.Nome,
            DataNascimento = DateTime.SpecifyKind(dto.DataNascimento.Date, DateTimeKind.Utc),
            TelefoneFicticio = dto.TelefoneFicticio,
            EmailFicticio = dto.EmailFicticio,
            CriadoEmUtc = DateTime.UtcNow
        };

        context.Pacientes.Add(paciente);
        await context.SaveChangesAsync();

        return paciente.ToDto();
    }

    public async Task<PacienteDto> AtualizarPacienteAsync(Guid id, AtualizarPacienteDto dto, UsuarioAtual solicitante)
    {
        var paciente = await context.Pacientes.FindAsync(id)
            ?? throw new RegraDeNegocioException("Paciente não encontrado.");

        if (solicitante.Perfil == PerfilUsuario.Paciente && paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Acesso negado.");
        }

        if (!string.IsNullOrWhiteSpace(dto.TelefoneFicticio))
            paciente.TelefoneFicticio = dto.TelefoneFicticio;

        if (!string.IsNullOrWhiteSpace(dto.EmailFicticio))
            paciente.EmailFicticio = dto.EmailFicticio;

        await context.SaveChangesAsync();

        return paciente.ToDto();
    }
}

// --- Admin ---
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
