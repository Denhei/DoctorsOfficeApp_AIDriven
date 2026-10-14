using Consultorio.Api.Models.Entities;

namespace Consultorio.Api.DTOs;

/// <summary>
/// Projeções entidade → DTO, uma por forma de resposta. As navegações lidas aqui precisam
/// estar carregadas (Include) ou atribuídas pelo chamador ao construir a entidade.
/// </summary>
public static class Mapeamentos
{
    public static ConsultaDto ToDto(this Consulta c) => new(
        c.Id,
        c.PacienteId,
        c.Paciente.Nome,
        c.Slot.MedicoId,
        c.Slot.Medico.Usuario.NomeCompleto,
        c.Slot.Medico.Especialidade,
        c.Slot.InstituicaoId,
        c.Slot.Instituicao.Nome,
        c.SlotId,
        c.Slot.InicioUtc,
        c.Slot.FimUtc,
        c.ColetaId,
        c.Coleta.ResumoValidado,
        c.Status,
        c.CriadaEmUtc,
        c.Evolucao != null,
        c.PesquisaSatisfacao != null);

    public static SlotAgendaDto ToDto(this SlotAgenda s, bool disponivel) => new(
        s.Id,
        s.MedicoId,
        s.Medico.Usuario.NomeCompleto,
        s.Medico.Especialidade,
        s.InstituicaoId,
        s.Instituicao.Nome,
        s.InicioUtc,
        s.FimUtc,
        disponivel);

    public static PacienteDto ToDto(this Paciente p) => new(
        p.Id, p.UsuarioId, p.Nome, p.DataNascimento, p.TelefoneFicticio, p.EmailFicticio, p.CriadoEmUtc);

    public static EvolucaoDto ToDto(this Evolucao e) => new(
        e.Id, e.ConsultaId, e.MedicoId, e.Medico.Usuario.NomeCompleto, e.Texto, e.CriadaEmUtc);

    public static DocumentoPacienteDto ToDto(this DocumentoPaciente d) => new(
        d.Id,
        d.PacienteId,
        d.InstituicaoOrigemId,
        d.InstituicaoOrigem.Nome,
        d.NomeOriginal,
        d.Tipo,
        d.Extensao,
        d.TamanhoBytes,
        d.CriadoEmUtc);

    public static AutorizacaoDto ToDto(this AutorizacaoCompartilhamento a) => new(
        a.Id,
        a.PacienteId,
        a.Paciente.Nome,
        a.InstituicaoOrigemId,
        a.InstituicaoOrigem.Nome,
        a.InstituicaoDestinoId,
        a.InstituicaoDestino.Nome,
        a.CriadaEmUtc,
        a.RevogadaEmUtc,
        a.RevogadaEmUtc == null);

    public static PesquisaSatisfacaoDto ToDto(this PesquisaSatisfacao p) => new(
        p.Id, p.ConsultaId, p.PacienteId, p.Nota, p.Percepcao, p.Comentario, p.RespondidaEmUtc);

    public static NotificacaoDto ToDto(this NotificacaoSimulada n) => new(
        n.Id, n.PacienteId, n.ConsultaId, n.Canal, n.Finalidade, n.Titulo, n.Texto, n.CriadaEmUtc, n.LidaEmUtc);
}
