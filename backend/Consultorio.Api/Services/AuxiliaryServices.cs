using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

// --- Notificações Simuladas & Pesquisa de Satisfação ---
public interface INotificacaoPesquisaService
{
    Task<List<NotificacaoDto>> ListarNotificacoesPacienteAsync(Guid pacienteUsuarioId);
    Task<PesquisaSatisfacaoDto?> ObterPesquisaConsultaAsync(Guid consultaId, Guid pacienteUsuarioId);
    Task<PesquisaSatisfacaoDto> ResponderPesquisaAsync(Guid consultaId, ResponderPesquisaDto dto, Guid pacienteUsuarioId);
}

public class NotificacaoPesquisaService : INotificacaoPesquisaService
{
    private readonly AppDbContext _context;

    public NotificacaoPesquisaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<NotificacaoDto>> ListarNotificacoesPacienteAsync(Guid pacienteUsuarioId)
    {
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.UsuarioId == pacienteUsuarioId)
            ?? throw new UnauthorizedAccessException("Paciente não encontrado.");

        var notifs = await _context.NotificacoesSimuladas
            .Where(n => n.PacienteId == paciente.Id)
            .OrderByDescending(n => n.CriadaEmUtc)
            .ToListAsync();

        return notifs.Select(n => new NotificacaoDto(
            n.Id,
            n.PacienteId,
            n.ConsultaId,
            n.Canal,
            n.Finalidade,
            n.Titulo,
            n.Texto,
            n.CriadaEmUtc,
            n.LidaEmUtc
        )).ToList();
    }

    public async Task<PesquisaSatisfacaoDto?> ObterPesquisaConsultaAsync(Guid consultaId, Guid pacienteUsuarioId)
    {
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.UsuarioId == pacienteUsuarioId)
            ?? throw new UnauthorizedAccessException("Paciente não encontrado.");

        var consulta = await _context.Consultas
            .Include(c => c.PesquisaSatisfacao)
            .FirstOrDefaultAsync(c => c.Id == consultaId && c.PacienteId == paciente.Id);

        if (consulta == null || consulta.PesquisaSatisfacao == null) return null;

        var p = consulta.PesquisaSatisfacao;
        return new PesquisaSatisfacaoDto(
            p.Id,
            p.ConsultaId,
            p.PacienteId,
            p.Nota,
            p.Percepcao,
            p.Comentario,
            p.RespondidaEmUtc
        );
    }

    public async Task<PesquisaSatisfacaoDto> ResponderPesquisaAsync(Guid consultaId, ResponderPesquisaDto dto, Guid pacienteUsuarioId)
    {
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.UsuarioId == pacienteUsuarioId)
            ?? throw new UnauthorizedAccessException("Paciente não encontrado.");

        var consulta = await _context.Consultas
            .Include(c => c.PesquisaSatisfacao)
            .FirstOrDefaultAsync(c => c.Id == consultaId && c.PacienteId == paciente.Id)
            ?? throw new InvalidOperationException("Consulta não encontrada.");

        if (consulta.Status != StatusConsulta.Concluida)
        {
            throw new InvalidOperationException("A pesquisa de satisfação só pode ser respondida após a conclusão da consulta.");
        }

        if (consulta.PesquisaSatisfacao != null)
        {
            throw new InvalidOperationException("Esta pesquisa de satisfação já foi respondida previamente.");
        }

        if (dto.Nota < 1 || dto.Nota > 5)
        {
            throw new InvalidOperationException("A nota de avaliação deve ser entre 1 e 5.");
        }

        var pesquisa = new PesquisaSatisfacao
        {
            Id = Guid.NewGuid(),
            ConsultaId = consultaId,
            PacienteId = paciente.Id,
            Nota = dto.Nota,
            Percepcao = dto.Percepcao,
            Comentario = dto.Comentario,
            RespondidaEmUtc = DateTime.UtcNow
        };

        await _context.PesquisasSatisfacao.AddAsync(pesquisa);
        await _context.SaveChangesAsync();

        return new PesquisaSatisfacaoDto(
            pesquisa.Id,
            pesquisa.ConsultaId,
            pesquisa.PacienteId,
            pesquisa.Nota,
            pesquisa.Percepcao,
            pesquisa.Comentario,
            pesquisa.RespondidaEmUtc
        );
    }
}

// --- Dashboard Operacional ---
public interface IDashboardService
{
    Task<DashboardOperacionalDto> ObterDashboardOperacionalAsync(Guid? instituicaoId);
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;

    public DashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardOperacionalDto> ObterDashboardOperacionalAsync(Guid? instituicaoId)
    {
        var queryConsultas = _context.Consultas.AsQueryable();
        var queryColetas = _context.ColetasIniciais.AsQueryable();
        var queryPesquisas = _context.PesquisasSatisfacao.AsQueryable();

        if (instituicaoId.HasValue)
        {
            queryConsultas = queryConsultas.Where(c => c.Slot.InstituicaoId == instituicaoId.Value);
            queryColetas = queryColetas.Where(c => c.InstituicaoId == instituicaoId.Value);
            queryPesquisas = queryPesquisas.Where(p => p.Consulta.Slot.InstituicaoId == instituicaoId.Value);
        }

        var totalAgendadas = await queryConsultas.CountAsync(c => c.Status == StatusConsulta.Agendada);
        var totalEmAtendimento = await queryConsultas.CountAsync(c => c.Status == StatusConsulta.EmAtendimento);
        var totalConcluidas = await queryConsultas.CountAsync(c => c.Status == StatusConsulta.Concluida);
        var totalCanceladas = await queryConsultas.CountAsync(c => c.Status == StatusConsulta.Cancelada);

        var totalColetasPendentes = await queryColetas.CountAsync(c => c.Status == StatusColeta.PendenteValidacao);

        var totalPesquisas = await queryPesquisas.CountAsync();
        var mediaSatisfacao = totalPesquisas > 0 
            ? await queryPesquisas.AverageAsync(p => p.Nota) 
            : 0.0;

        return new DashboardOperacionalDto(
            totalAgendadas,
            totalEmAtendimento,
            totalConcluidas,
            totalCanceladas,
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

public class InstituicaoService : IInstituicaoService
{
    private readonly AppDbContext _context;

    public InstituicaoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<InstituicaoDto>> ListarInstituicoesAsync()
    {
        return await _context.Instituicoes
            .Where(i => i.Ativa)
            .Select(i => new InstituicaoDto(i.Id, i.Nome, i.RazaoSocial, i.Ativa))
            .ToListAsync();
    }

    public async Task<List<MedicoDto>> ListarMedicosAsync(Guid? instituicaoId)
    {
        var query = _context.Medicos
            .Include(m => m.Usuario)
            .Include(m => m.Instituicao)
            .AsQueryable();

        if (instituicaoId.HasValue)
        {
            query = query.Where(m => m.InstituicaoId == instituicaoId.Value);
        }

        return await query.Select(m => new MedicoDto(
            m.Id,
            m.UsuarioId,
            m.Usuario.NomeCompleto,
            m.Especialidade,
            m.CrmFicticio,
            m.InstituicaoId,
            m.Instituicao.Nome
        )).ToListAsync();
    }
}

// --- Pacientes ---
public interface IPacienteService
{
    Task<List<PacienteDto>> ListarPacientesAsync();
    Task<PacienteDto?> ObterPacientePorIdAsync(Guid id, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<PacienteDto> CriarPacienteAsync(CriarPacienteDto dto);
    Task<PacienteDto> AtualizarPacienteAsync(Guid id, AtualizarPacienteDto dto, Guid solicitanteUsuarioId, PerfilUsuario perfil);
}

public class PacienteService : IPacienteService
{
    private readonly AppDbContext _context;
    private readonly UserManager<Usuario> _userManager;

    public PacienteService(AppDbContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<PacienteDto>> ListarPacientesAsync()
    {
        return await _context.Pacientes
            .Select(p => new PacienteDto(
                p.Id,
                p.UsuarioId,
                p.Nome,
                p.DataNascimento,
                p.TelefoneFicticio,
                p.EmailFicticio,
                p.CriadoEmUtc
            ))
            .ToListAsync();
    }

    public async Task<PacienteDto?> ObterPacientePorIdAsync(Guid id, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        var p = await _context.Pacientes.FindAsync(id);
        if (p == null) return null;

        if (perfil == PerfilUsuario.Paciente && p.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado aos dados de outro paciente.");
        }

        return new PacienteDto(
            p.Id,
            p.UsuarioId,
            p.Nome,
            p.DataNascimento,
            p.TelefoneFicticio,
            p.EmailFicticio,
            p.CriadoEmUtc
        );
    }

    public async Task<PacienteDto> CriarPacienteAsync(CriarPacienteDto dto)
    {
        var user = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = dto.EmailFicticio,
            Email = dto.EmailFicticio,
            NomeCompleto = dto.Nome,
            Perfil = PerfilUsuario.Paciente,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Senha);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Erro ao criar usuário: {errors}");
        }

        await _userManager.AddToRoleAsync(user, PerfilUsuario.Paciente.ToString());

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            UsuarioId = user.Id,
            Nome = dto.Nome,
            DataNascimento = DateTime.SpecifyKind(dto.DataNascimento.Date, DateTimeKind.Utc),
            TelefoneFicticio = dto.TelefoneFicticio,
            EmailFicticio = dto.EmailFicticio,
            CriadoEmUtc = DateTime.UtcNow
        };

        await _context.Pacientes.AddAsync(paciente);
        await _context.SaveChangesAsync();

        return new PacienteDto(
            paciente.Id,
            paciente.UsuarioId,
            paciente.Nome,
            paciente.DataNascimento,
            paciente.TelefoneFicticio,
            paciente.EmailFicticio,
            paciente.CriadoEmUtc
        );
    }

    public async Task<PacienteDto> AtualizarPacienteAsync(Guid id, AtualizarPacienteDto dto, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        var p = await _context.Pacientes.FindAsync(id)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (perfil == PerfilUsuario.Paciente && p.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        if (!string.IsNullOrWhiteSpace(dto.TelefoneFicticio))
            p.TelefoneFicticio = dto.TelefoneFicticio;

        if (!string.IsNullOrWhiteSpace(dto.EmailFicticio))
            p.EmailFicticio = dto.EmailFicticio;

        await _context.SaveChangesAsync();

        return new PacienteDto(
            p.Id,
            p.UsuarioId,
            p.Nome,
            p.DataNascimento,
            p.TelefoneFicticio,
            p.EmailFicticio,
            p.CriadoEmUtc
        );
    }
}

// --- Admin ---
public interface IAdminService
{
    Task<List<UsuarioInfoDto>> ListarUsuariosAsync();
    Task<UsuarioInfoDto> CriarUsuarioAsync(CriarUsuarioAdminDto dto);
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _context;
    private readonly UserManager<Usuario> _userManager;

    public AdminService(AppDbContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<UsuarioInfoDto>> ListarUsuariosAsync()
    {
        var usuarios = await _userManager.Users
            .Include(u => u.Instituicao)
            .ToListAsync();

        var lista = new List<UsuarioInfoDto>();
        foreach (var u in usuarios)
        {
            Guid? pacId = null;
            Guid? medId = null;

            if (u.Perfil == PerfilUsuario.Paciente)
            {
                var p = await _context.Pacientes.FirstOrDefaultAsync(pac => pac.UsuarioId == u.Id);
                pacId = p?.Id;
            }
            else if (u.Perfil == PerfilUsuario.Medico)
            {
                var m = await _context.Medicos.FirstOrDefaultAsync(med => med.UsuarioId == u.Id);
                medId = m?.Id;
            }

            lista.Add(new UsuarioInfoDto(
                u.Id,
                u.NomeCompleto,
                u.Email ?? "",
                u.Perfil,
                u.InstituicaoId,
                u.Instituicao?.Nome,
                pacId,
                medId
            ));
        }

        return lista;
    }

    public async Task<UsuarioInfoDto> CriarUsuarioAsync(CriarUsuarioAdminDto dto)
    {
        var user = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = dto.Email,
            Email = dto.Email,
            NomeCompleto = dto.NomeCompleto,
            Perfil = dto.Perfil,
            InstituicaoId = dto.InstituicaoId,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };

        var res = await _userManager.CreateAsync(user, dto.Senha);
        if (!res.Succeeded)
        {
            var errors = string.Join(", ", res.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Erro ao criar usuário: {errors}");
        }

        await _userManager.AddToRoleAsync(user, dto.Perfil.ToString());

        Guid? pacId = null;
        Guid? medId = null;

        if (dto.Perfil == PerfilUsuario.Medico)
        {
            if (!dto.InstituicaoId.HasValue)
                throw new InvalidOperationException("Médico deve ter uma instituição vinculada.");

            var med = new Medico
            {
                Id = Guid.NewGuid(),
                UsuarioId = user.Id,
                InstituicaoId = dto.InstituicaoId.Value,
                Especialidade = dto.EspecialidadeMedico ?? "Clínica Geral",
                CrmFicticio = dto.CrmMedico ?? "CRM/SP 000000"
            };
            await _context.Medicos.AddAsync(med);
            await _context.SaveChangesAsync();
            medId = med.Id;
        }

        var inst = dto.InstituicaoId.HasValue ? await _context.Instituicoes.FindAsync(dto.InstituicaoId.Value) : null;

        return new UsuarioInfoDto(
            user.Id,
            user.NomeCompleto,
            user.Email ?? "",
            user.Perfil,
            user.InstituicaoId,
            inst?.Nome,
            pacId,
            medId
        );
    }
}
