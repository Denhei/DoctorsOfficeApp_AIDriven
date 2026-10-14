using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Infraestrutura;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IAgendaService
{
    Task<List<SlotAgendaDto>> ListarSlotsAsync(Guid? medicoId, Guid? instituicaoId, DateTime? data);
    Task<SlotAgendaDto> CriarSlotAsync(CriarSlotDto dto, Guid instituicaoId);
    Task<ConsultaDto> AgendarConsultaAsync(AgendarConsultaDto dto, UsuarioAtual solicitante);
    Task<List<ConsultaDto>> ListarConsultasAsync(UsuarioAtual solicitante, Guid? instituicaoId, Guid? pacienteId);
    Task<ConsultaDto?> ObterConsultaPorIdAsync(Guid consultaId, UsuarioAtual solicitante);
    Task<bool> AtualizarStatusConsultaAsync(Guid consultaId, StatusConsulta novoStatus, UsuarioAtual solicitante);
}

public class AgendaService(AppDbContext context) : IAgendaService
{
    public async Task<List<SlotAgendaDto>> ListarSlotsAsync(Guid? medicoId, Guid? instituicaoId, DateTime? data)
    {
        var query = context.SlotsAgenda
            .Include(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(s => s.Instituicao)
            .Include(s => s.Consultas)
            .AsQueryable();

        if (medicoId.HasValue)
        {
            query = query.Where(s => s.MedicoId == medicoId.Value);
        }

        if (instituicaoId.HasValue)
        {
            query = query.Where(s => s.InstituicaoId == instituicaoId.Value);
        }

        if (data.HasValue)
        {
            var inicioDia = DateTime.SpecifyKind(data.Value.Date, DateTimeKind.Utc);
            var fimDia = inicioDia.AddDays(1);
            query = query.Where(s => s.InicioUtc >= inicioDia && s.InicioUtc < fimDia);
        }

        var slots = await query.OrderBy(s => s.InicioUtc).ToListAsync();

        return slots
            .Select(s => s.ToDto(disponivel: !s.Consultas.Any(c => c.Status != StatusConsulta.Cancelada)))
            .ToList();
    }

    public async Task<SlotAgendaDto> CriarSlotAsync(CriarSlotDto dto, Guid instituicaoId)
    {
        var medico = await context.Medicos
            .Include(m => m.Usuario)
            .Include(m => m.Instituicao)
            .FirstOrDefaultAsync(m => m.Id == dto.MedicoId && m.InstituicaoId == instituicaoId)
            ?? throw new InvalidOperationException("Médico não encontrado ou não pertence a esta instituição.");

        if (dto.FimUtc <= dto.InicioUtc)
        {
            throw new InvalidOperationException("Horário de fim deve ser posterior ao início.");
        }

        var sobreposicao = await context.SlotsAgenda
            .AnyAsync(s => s.MedicoId == dto.MedicoId && s.InicioUtc < dto.FimUtc && s.FimUtc > dto.InicioUtc);

        if (sobreposicao)
        {
            throw new InvalidOperationException("Já existe um slot cadastrado para este médico no mesmo horário.");
        }

        var slot = new SlotAgenda
        {
            MedicoId = medico.Id,
            Medico = medico,
            InstituicaoId = instituicaoId,
            Instituicao = medico.Instituicao,
            InicioUtc = dto.InicioUtc,
            FimUtc = dto.FimUtc
        };

        context.SlotsAgenda.Add(slot);
        await context.SaveChangesAsync();

        return slot.ToDto(disponivel: true);
    }

    public async Task<ConsultaDto> AgendarConsultaAsync(AgendarConsultaDto dto, UsuarioAtual solicitante)
    {
        // Transação com lock na linha do paciente: evita dupla reserva do slot e sobreposição de horário do paciente.
        using var transaction = await context.Database.BeginTransactionAsync();

        var paciente = await context.Pacientes
            .FromSqlRaw("SELECT * FROM \"Pacientes\" WHERE \"Id\" = {0} FOR UPDATE", dto.PacienteId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (solicitante.Perfil == PerfilUsuario.Paciente && paciente.UsuarioId != solicitante.Id)
        {
            throw new UnauthorizedAccessException("Você só pode agendar consultas para seu próprio cadastro.");
        }

        var coleta = await context.ColetasIniciais.FirstOrDefaultAsync(c => c.Id == dto.ColetaId && c.PacienteId == dto.PacienteId)
            ?? throw new InvalidOperationException("Coleta inicial não encontrada.");

        if (coleta.Status != StatusColeta.Validada)
        {
            throw new InvalidOperationException("O agendamento só é permitido após a validação da triagem pelo enfermeiro.");
        }

        var slot = await context.SlotsAgenda
            .Include(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(s => s.Instituicao)
            .FirstOrDefaultAsync(s => s.Id == dto.SlotId)
            ?? throw new InvalidOperationException("Slot de agenda não encontrado.");

        if (slot.InstituicaoId != coleta.InstituicaoId)
        {
            throw new InvalidOperationException("A instituição do médico escolhido deve ser a mesma da coleta validada.");
        }

        if (slot.InicioUtc <= DateTime.UtcNow.AddMinutes(-5))
        {
            throw new InvalidOperationException("Não é possível agendar consultas em horários passados.");
        }

        var slotOcupado = await context.Consultas
            .AnyAsync(c => c.SlotId == dto.SlotId && c.Status != StatusConsulta.Cancelada);

        if (slotOcupado)
        {
            throw new ConflitoException("Este horário já foi reservado por outro paciente (Conflito de Slot).");
        }

        var pacienteConflito = await context.Consultas
            .AnyAsync(c => c.PacienteId == dto.PacienteId
                        && c.Status != StatusConsulta.Cancelada
                        && c.Slot.InicioUtc < slot.FimUtc
                        && c.Slot.FimUtc > slot.InicioUtc);

        if (pacienteConflito)
        {
            throw new ConflitoException("O paciente já possui uma consulta agendada neste mesmo intervalo de horário.");
        }

        var consulta = new Consulta
        {
            PacienteId = paciente.Id,
            Paciente = paciente,
            SlotId = slot.Id,
            Slot = slot,
            ColetaId = coleta.Id,
            Coleta = coleta,
            Status = StatusConsulta.Agendada,
            CriadaEmUtc = DateTime.UtcNow
        };

        context.Consultas.Add(consulta);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return consulta.ToDto();
    }

    public async Task<List<ConsultaDto>> ListarConsultasAsync(UsuarioAtual solicitante, Guid? instituicaoId, Guid? pacienteId)
    {
        var query = context.Consultas.ComDetalhes();

        switch (solicitante.Perfil)
        {
            case PerfilUsuario.Paciente:
                if (solicitante.PacienteId is not { } pacId) return [];
                query = query.Where(c => c.PacienteId == pacId);
                break;

            case PerfilUsuario.Medico:
                if (solicitante.MedicoId is not { } medId) return [];
                query = query.Where(c => c.Slot.MedicoId == medId);
                break;

            case PerfilUsuario.Atendente:
            case PerfilUsuario.Enfermeiro:
                if (solicitante.InstituicaoId is { } instId)
                {
                    query = query.Where(c => c.Slot.InstituicaoId == instId);
                }
                break;
        }

        if (instituicaoId.HasValue && solicitante.Perfil == PerfilUsuario.Admin)
        {
            query = query.Where(c => c.Slot.InstituicaoId == instituicaoId.Value);
        }

        if (pacienteId.HasValue && solicitante.Perfil != PerfilUsuario.Paciente)
        {
            query = query.Where(c => c.PacienteId == pacienteId.Value);
        }

        var consultas = await query.OrderByDescending(c => c.Slot.InicioUtc).ToListAsync();

        return consultas.Select(c => c.ToDto()).ToList();
    }

    public async Task<ConsultaDto?> ObterConsultaPorIdAsync(Guid consultaId, UsuarioAtual solicitante)
    {
        var consulta = await context.Consultas.ComDetalhes().FirstOrDefaultAsync(c => c.Id == consultaId);

        if (consulta == null) return null;

        if (solicitante.Perfil == PerfilUsuario.Paciente && consulta.Paciente.UsuarioId != solicitante.Id)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        // Qualquer médico cadastrado pode ler a consulta; só é negado o perfil Medico sem registro de Medico.
        if (solicitante.Perfil == PerfilUsuario.Medico && solicitante.MedicoId is null)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        return consulta.ToDto();
    }

    public async Task<bool> AtualizarStatusConsultaAsync(Guid consultaId, StatusConsulta novoStatus, UsuarioAtual solicitante)
    {
        var consulta = await context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Slot).ThenInclude(s => s.Medico)
            .FirstOrDefaultAsync(c => c.Id == consultaId);

        if (consulta == null) return false;

        switch (novoStatus)
        {
            case StatusConsulta.Cancelada:
                if (solicitante.Perfil == PerfilUsuario.Paciente && consulta.Paciente.UsuarioId != solicitante.Id)
                    throw new UnauthorizedAccessException("Acesso negado.");

                if (consulta.Status == StatusConsulta.Concluida)
                    throw new InvalidOperationException("Consultas já concluídas não podem ser canceladas.");
                break;

            case StatusConsulta.EmAtendimento:
                if (solicitante.Perfil != PerfilUsuario.Medico || consulta.Slot.Medico.UsuarioId != solicitante.Id)
                    throw new UnauthorizedAccessException("Apenas o médico responsável pode iniciar o atendimento.");
                break;

            default:
                return false;
        }

        consulta.Status = novoStatus;
        await context.SaveChangesAsync();
        return true;
    }
}
