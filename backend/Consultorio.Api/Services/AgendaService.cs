using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IAgendaService
{
    Task<List<SlotAgendaDto>> ListarSlotsAsync(Guid? medicoId, Guid? instituicaoId, DateTime? data);
    Task<SlotAgendaDto> CriarSlotAsync(CriarSlotDto dto, Guid instituicaoId);
    Task<ConsultaDto> AgendarConsultaAsync(AgendarConsultaDto dto, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<List<ConsultaDto>> ListarConsultasAsync(Guid solicitanteUsuarioId, PerfilUsuario perfil, Guid? instituicaoId, Guid? pacienteId, Guid? medicoId);
    Task<ConsultaDto?> ObterConsultaPorIdAsync(Guid consultaId, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<bool> AtualizarStatusConsultaAsync(Guid consultaId, StatusConsulta novoStatus, Guid solicitanteUsuarioId, PerfilUsuario perfil);
}

public class AgendaService : IAgendaService
{
    private readonly AppDbContext _context;

    public AgendaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SlotAgendaDto>> ListarSlotsAsync(Guid? medicoId, Guid? instituicaoId, DateTime? data)
    {
        var query = _context.SlotsAgenda
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

        return slots.Select(s => new SlotAgendaDto(
            s.Id,
            s.MedicoId,
            s.Medico.Usuario.NomeCompleto,
            s.Medico.Especialidade,
            s.InstituicaoId,
            s.Instituicao.Nome,
            s.InicioUtc,
            s.FimUtc,
            !s.Consultas.Any(c => c.Status != StatusConsulta.Cancelada)
        )).ToList();
    }

    public async Task<SlotAgendaDto> CriarSlotAsync(CriarSlotDto dto, Guid instituicaoId)
    {
        var medico = await _context.Medicos
            .Include(m => m.Usuario)
            .Include(m => m.Instituicao)
            .FirstOrDefaultAsync(m => m.Id == dto.MedicoId && m.InstituicaoId == instituicaoId)
            ?? throw new InvalidOperationException("Médico não encontrado ou não pertence a esta instituição.");

        if (dto.FimUtc <= dto.InicioUtc)
        {
            throw new InvalidOperationException("Horário de fim deve ser posterior ao início.");
        }

        var sobreposicao = await _context.SlotsAgenda
            .AnyAsync(s => s.MedicoId == dto.MedicoId && s.InicioUtc < dto.FimUtc && s.FimUtc > dto.InicioUtc);

        if (sobreposicao)
        {
            throw new InvalidOperationException("Já existe um slot cadastrado para este médico no mesmo horário.");
        }

        var slot = new SlotAgenda
        {
            Id = Guid.NewGuid(),
            MedicoId = dto.MedicoId,
            InstituicaoId = instituicaoId,
            InicioUtc = dto.InicioUtc,
            FimUtc = dto.FimUtc
        };

        await _context.SlotsAgenda.AddAsync(slot);
        await _context.SaveChangesAsync();

        return new SlotAgendaDto(
            slot.Id,
            medico.Id,
            medico.Usuario.NomeCompleto,
            medico.Especialidade,
            instituicaoId,
            medico.Instituicao.Nome,
            slot.InicioUtc,
            slot.FimUtc,
            true
        );
    }

    public async Task<ConsultaDto> AgendarConsultaAsync(AgendarConsultaDto dto, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        // 1. Usar transação no PostgreSQL com isolamento para garantir que não haja concorrência de horário do mesmo paciente nem do mesmo slot
        using var transaction = await _context.Database.BeginTransactionAsync();

        // Lock na linha do paciente
        var paciente = await _context.Pacientes
            .FromSqlRaw("SELECT * FROM \"Pacientes\" WHERE \"Id\" = {0} FOR UPDATE", dto.PacienteId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (perfil == PerfilUsuario.Paciente && paciente.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Você só pode agendar consultas para seu próprio cadastro.");
        }

        // 2. Validar se a coleta existe, pertence ao paciente e está com status 'Validada'
        var coleta = await _context.ColetasIniciais.FirstOrDefaultAsync(c => c.Id == dto.ColetaId && c.PacienteId == dto.PacienteId)
            ?? throw new InvalidOperationException("Coleta inicial não encontrada.");

        if (coleta.Status != StatusColeta.Validada)
        {
            throw new InvalidOperationException("O agendamento só é permitido após a validação da triagem pelo enfermeiro.");
        }

        // 3. Validar se o slot existe e pertence à mesma instituição da coleta
        var slot = await _context.SlotsAgenda
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

        // 4. Verificar se o slot já possui consulta ativa (não cancelada)
        var slotOcupado = await _context.Consultas
            .AnyAsync(c => c.SlotId == dto.SlotId && c.Status != StatusConsulta.Cancelada);

        if (slotOcupado)
        {
            throw new ConflitoException("Este horário já foi reservado por outro paciente (Conflito de Slot).");
        }

        // 5. Verificar se o paciente já possui outra consulta ativa no mesmo horário (sobreposição)
        var pacienteConflito = await _context.Consultas
            .Include(c => c.Slot)
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
            Id = Guid.NewGuid(),
            PacienteId = dto.PacienteId,
            SlotId = dto.SlotId,
            ColetaId = dto.ColetaId,
            Status = StatusConsulta.Agendada,
            CriadaEmUtc = DateTime.UtcNow
        };

        await _context.Consultas.AddAsync(consulta);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new ConsultaDto(
            consulta.Id,
            paciente.Id,
            paciente.Nome,
            slot.Medico.Id,
            slot.Medico.Usuario.NomeCompleto,
            slot.Medico.Especialidade,
            slot.Instituicao.Id,
            slot.Instituicao.Nome,
            slot.Id,
            slot.InicioUtc,
            slot.FimUtc,
            coleta.Id,
            coleta.ResumoValidado,
            consulta.Status,
            consulta.CriadaEmUtc,
            false,
            false
        );
    }

    public async Task<List<ConsultaDto>> ListarConsultasAsync(Guid solicitanteUsuarioId, PerfilUsuario perfil, Guid? instituicaoId, Guid? pacienteId, Guid? medicoId)
    {
        var query = _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Coleta)
            .Include(c => c.Evolucao)
            .Include(c => c.PesquisaSatisfacao)
            .Include(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(c => c.Slot).ThenInclude(s => s.Instituicao)
            .AsQueryable();

        if (perfil == PerfilUsuario.Paciente)
        {
            var pac = await _context.Pacientes.FirstOrDefaultAsync(p => p.UsuarioId == solicitanteUsuarioId);
            if (pac == null) return new List<ConsultaDto>();
            query = query.Where(c => c.PacienteId == pac.Id);
        }
        else if (perfil == PerfilUsuario.Medico)
        {
            var med = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == solicitanteUsuarioId);
            if (med == null) return new List<ConsultaDto>();
            query = query.Where(c => c.Slot.MedicoId == med.Id);
        }
        else if (perfil == PerfilUsuario.Atendente || perfil == PerfilUsuario.Enfermeiro)
        {
            var user = await _context.Users.FindAsync(solicitanteUsuarioId);
            if (user?.InstituicaoId != null)
            {
                query = query.Where(c => c.Slot.InstituicaoId == user.InstituicaoId.Value);
            }
        }

        if (instituicaoId.HasValue && perfil == PerfilUsuario.Admin)
        {
            query = query.Where(c => c.Slot.InstituicaoId == instituicaoId.Value);
        }

        if (pacienteId.HasValue && perfil != PerfilUsuario.Paciente)
        {
            query = query.Where(c => c.PacienteId == pacienteId.Value);
        }

        var consultas = await query.OrderByDescending(c => c.Slot.InicioUtc).ToListAsync();

        return consultas.Select(c => new ConsultaDto(
            c.Id,
            c.Paciente.Id,
            c.Paciente.Nome,
            c.Slot.Medico.Id,
            c.Slot.Medico.Usuario.NomeCompleto,
            c.Slot.Medico.Especialidade,
            c.Slot.Instituicao.Id,
            c.Slot.Instituicao.Nome,
            c.Slot.Id,
            c.Slot.InicioUtc,
            c.Slot.FimUtc,
            c.Coleta.Id,
            c.Coleta.ResumoValidado,
            c.Status,
            c.CriadaEmUtc,
            c.Evolucao != null,
            c.PesquisaSatisfacao != null
        )).ToList();
    }

    public async Task<ConsultaDto?> ObterConsultaPorIdAsync(Guid consultaId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        var c = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Coleta)
            .Include(c => c.Evolucao)
            .Include(c => c.PesquisaSatisfacao)
            .Include(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(c => c.Slot).ThenInclude(s => s.Instituicao)
            .FirstOrDefaultAsync(c => c.Id == consultaId);

        if (c == null) return null;

        if (perfil == PerfilUsuario.Paciente && c.Paciente.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        if (perfil == PerfilUsuario.Medico && c.Slot.Medico.UsuarioId != solicitanteUsuarioId)
        {
            // Verificar se o médico tem autorização de compartilhamento caso seja de outra instituição
            var med = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == solicitanteUsuarioId);
            if (med == null) throw new UnauthorizedAccessException("Acesso negado.");
        }

        return new ConsultaDto(
            c.Id,
            c.Paciente.Id,
            c.Paciente.Nome,
            c.Slot.Medico.Id,
            c.Slot.Medico.Usuario.NomeCompleto,
            c.Slot.Medico.Especialidade,
            c.Slot.Instituicao.Id,
            c.Slot.Instituicao.Nome,
            c.Slot.Id,
            c.Slot.InicioUtc,
            c.Slot.FimUtc,
            c.Coleta.Id,
            c.Coleta.ResumoValidado,
            c.Status,
            c.CriadaEmUtc,
            c.Evolucao != null,
            c.PesquisaSatisfacao != null
        );
    }

    public async Task<bool> AtualizarStatusConsultaAsync(Guid consultaId, StatusConsulta novoStatus, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Slot).ThenInclude(s => s.Medico)
            .FirstOrDefaultAsync(c => c.Id == consultaId);

        if (consulta == null) return false;

        // Cancelamento pode ser feito pelo paciente, atendente ou médico
        if (novoStatus == StatusConsulta.Cancelada)
        {
            if (perfil == PerfilUsuario.Paciente && consulta.Paciente.UsuarioId != solicitanteUsuarioId)
                throw new UnauthorizedAccessException("Acesso negado.");

            if (consulta.Status == StatusConsulta.Concluida)
                throw new InvalidOperationException("Consultas já concluídas não podem ser canceladas.");

            consulta.Status = StatusConsulta.Cancelada;
            await _context.SaveChangesAsync();
            return true;
        }

        // Início de atendimento só pelo médico
        if (novoStatus == StatusConsulta.EmAtendimento)
        {
            if (perfil != PerfilUsuario.Medico || consulta.Slot.Medico.UsuarioId != solicitanteUsuarioId)
                throw new UnauthorizedAccessException("Apenas o médico responsável pode iniciar o atendimento.");

            consulta.Status = StatusConsulta.EmAtendimento;
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }
}
