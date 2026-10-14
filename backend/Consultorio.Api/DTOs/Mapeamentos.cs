using System.Linq.Expressions;
using Consultorio.Api.Data;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Models.Mongo;

namespace Consultorio.Api.DTOs;

/// <summary>
/// Um único ponto de montagem por DTO. As projeções são expressões para que o EF Core traduza
/// o mapeamento para SQL (Select); as versões <c>ToDto()</c> são as mesmas expressões compiladas,
/// para agregados já carregados em memória.
/// </summary>
public static class Mapeamentos
{
    public static readonly Expression<Func<Instituicao, InstituicaoDto>> Instituicao =
        i => new InstituicaoDto(i.Id, i.Nome, i.RazaoSocial, i.Ativa);

    public static readonly Expression<Func<Medico, MedicoDto>> Medico =
        m => new MedicoDto(m.Id, m.UsuarioId, m.Usuario.NomeCompleto, m.Especialidade, m.CrmFicticio, m.InstituicaoId, m.Instituicao.Nome);

    public static readonly Expression<Func<Paciente, PacienteDto>> Paciente =
        p => new PacienteDto(p.Id, p.UsuarioId, p.Nome, p.DataNascimento, p.TelefoneFicticio, p.EmailFicticio, p.CriadoEmUtc);

    /// <summary>Disponível quando não há consulta ativa (não cancelada) no slot; vira NOT EXISTS no SQL.</summary>
    public static readonly Expression<Func<SlotAgenda, SlotAgendaDto>> Slot =
        s => new SlotAgendaDto(
            s.Id,
            s.MedicoId,
            s.Medico.Usuario.NomeCompleto,
            s.Medico.Especialidade,
            s.InstituicaoId,
            s.Instituicao.Nome,
            s.InicioUtc,
            s.FimUtc,
            !s.Consultas.Any(c => c.Status != StatusConsulta.Cancelada));

    public static readonly Expression<Func<Consulta, ConsultaDto>> Consulta =
        c => new ConsultaDto(
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

    public static readonly Expression<Func<Evolucao, EvolucaoDto>> Evolucao =
        e => new EvolucaoDto(e.Id, e.ConsultaId, e.MedicoId, e.Medico.Usuario.NomeCompleto, e.Texto, e.CriadaEmUtc);

    public static readonly Expression<Func<DocumentoPaciente, DocumentoPacienteDto>> Documento =
        d => new DocumentoPacienteDto(
            d.Id,
            d.PacienteId,
            d.InstituicaoOrigemId,
            d.InstituicaoOrigem.Nome,
            d.NomeOriginal,
            d.Tipo,
            d.Extensao,
            d.TamanhoBytes,
            d.CriadoEmUtc);

    public static readonly Expression<Func<AutorizacaoCompartilhamento, AutorizacaoDto>> Autorizacao =
        a => new AutorizacaoDto(
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

    public static readonly Expression<Func<PesquisaSatisfacao, PesquisaSatisfacaoDto>> Pesquisa =
        p => new PesquisaSatisfacaoDto(p.Id, p.ConsultaId, p.PacienteId, p.Nota, p.Percepcao, p.Comentario, p.RespondidaEmUtc);

    public static readonly Expression<Func<NotificacaoSimulada, NotificacaoDto>> Notificacao =
        n => new NotificacaoDto(n.Id, n.PacienteId, n.ConsultaId, n.Canal, n.Finalidade, n.Titulo, n.Texto, n.CriadaEmUtc, n.LidaEmUtc);

    /// <summary>PacienteId e MedicoId resolvidos por subconsulta, sem uma ida ao banco por usuário.</summary>
    public static Expression<Func<Usuario, UsuarioInfoDto>> UsuarioInfo(AppDbContext db) =>
        u => new UsuarioInfoDto(
            u.Id,
            u.NomeCompleto,
            u.Email ?? string.Empty,
            u.Perfil,
            u.InstituicaoId,
            u.Instituicao != null ? u.Instituicao.Nome : null,
            db.Pacientes.Where(p => p.UsuarioId == u.Id).Select(p => (Guid?)p.Id).FirstOrDefault(),
            db.Medicos.Where(m => m.UsuarioId == u.Id).Select(m => (Guid?)m.Id).FirstOrDefault());

    // Em memória: as navegações lidas pela expressão precisam estar carregadas.
    private static readonly Func<Paciente, PacienteDto> PacienteEmMemoria = Paciente.Compile();
    private static readonly Func<Consulta, ConsultaDto> ConsultaEmMemoria = Consulta.Compile();
    private static readonly Func<Evolucao, EvolucaoDto> EvolucaoEmMemoria = Evolucao.Compile();
    private static readonly Func<DocumentoPaciente, DocumentoPacienteDto> DocumentoEmMemoria = Documento.Compile();
    private static readonly Func<AutorizacaoCompartilhamento, AutorizacaoDto> AutorizacaoEmMemoria = Autorizacao.Compile();
    private static readonly Func<PesquisaSatisfacao, PesquisaSatisfacaoDto> PesquisaEmMemoria = Pesquisa.Compile();

    public static PacienteDto ToDto(this Paciente p) => PacienteEmMemoria(p);
    public static ConsultaDto ToDto(this Consulta c) => ConsultaEmMemoria(c);
    public static EvolucaoDto ToDto(this Evolucao e) => EvolucaoEmMemoria(e);
    public static DocumentoPacienteDto ToDto(this DocumentoPaciente d) => DocumentoEmMemoria(d);
    public static AutorizacaoDto ToDto(this AutorizacaoCompartilhamento a) => AutorizacaoEmMemoria(a);
    public static PesquisaSatisfacaoDto ToDto(this PesquisaSatisfacao p) => PesquisaEmMemoria(p);

    // Triagem: combina a coleta (PostgreSQL) com a conversa (MongoDB).
    public static ColetaItemFilaDto ToFilaDto(this ColetaInicial c, ConversaMongo? conversa) => new(
        c.Id,
        c.PacienteId,
        c.Paciente.Nome,
        c.InstituicaoId,
        c.Instituicao.Nome,
        c.Status,
        c.CriadaEmUtc,
        conversa?.ResumoPreliminar.GerarTextoFormatado());

    public static ConversaDetalheDto ToDetalheDto(this ColetaInicial coleta, ConversaMongo conversa) => new(
        coleta.Id,
        conversa.Id,
        coleta.PacienteId,
        coleta.Paciente.Nome,
        coleta.InstituicaoId,
        coleta.Instituicao.Nome,
        coleta.Status,
        conversa.EtapaAtual,
        new ResumoPreliminarDto(
            conversa.ResumoPreliminar.Queixa,
            conversa.ResumoPreliminar.Duracao,
            conversa.ResumoPreliminar.Alergias,
            conversa.ResumoPreliminar.Medicamentos,
            conversa.ResumoPreliminar.Observacoes),
        coleta.ResumoValidado,
        conversa.Mensagens.Select(m => new MensagemChatDto(m.Id, m.Remetente, m.Conteudo, m.Tipo, m.Opcoes, m.EnviadaEmUtc)).ToList(),
        conversa.CriadaEmUtc);
}
