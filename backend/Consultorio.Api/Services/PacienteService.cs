using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

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
