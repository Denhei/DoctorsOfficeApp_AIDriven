using Consultorio.Api.Models.Enums;

namespace Consultorio.Api.DTOs;

// DTOs de entrada usam init e valor padrão explícito para que um campo ausente no JSON
// chegue como "" e não como null.

// --- Instituição & Médico ---
public record InstituicaoDto(Guid Id, string Nome, string RazaoSocial, bool Ativa);

public record MedicoDto(Guid Id, Guid UsuarioId, string Nome, string Especialidade, string CrmFicticio, Guid InstituicaoId, string InstituicaoNome);

// --- Paciente ---
public record PacienteDto(Guid Id, Guid UsuarioId, string Nome, DateTime DataNascimento, string TelefoneFicticio, string EmailFicticio, DateTime CriadoEmUtc);

public record CriarPacienteDto
{
    public string Nome { get; init; } = string.Empty;
    public DateTime DataNascimento { get; init; }
    public string TelefoneFicticio { get; init; } = string.Empty;
    public string EmailFicticio { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}

public record AtualizarPacienteDto
{
    public string? TelefoneFicticio { get; init; }
    public string? EmailFicticio { get; init; }
}

// --- Coleta & Chat ---
public record IniciarColetaDto
{
    public Guid InstituicaoId { get; init; }
}

public record EnviarMensagemChatDto
{
    public string Conteudo { get; init; } = string.Empty;
    public string? ClienteMessageId { get; init; }
}

public record ResumoPreliminarDto(
    string Queixa,
    string Duracao,
    string Alergias,
    string Medicamentos,
    string Observacoes
);

public record MensagemChatDto(
    string Id,
    string Remetente,
    string Conteudo,
    string Tipo,
    List<string>? Opcoes,
    DateTime EnviadaEmUtc
);

public record ConversaDetalheDto(
    Guid ColetaId,
    string ConversaId,
    Guid PacienteId,
    string PacienteNome,
    Guid InstituicaoId,
    string InstituicaoNome,
    StatusColeta StatusColeta,
    int EtapaAtual,
    ResumoPreliminarDto ResumoPreliminar,
    string? ResumoValidado,
    List<MensagemChatDto> Mensagens,
    DateTime CriadaEmUtc
);

public record ValidarColetaDto
{
    /// <summary>true leva a Validada (libera agendamento); false leva a EncaminhadaPresencial.</summary>
    public bool AprovadoParaAgendamento { get; init; }
    public string ResumoCorrigido { get; init; } = string.Empty;
    public string? ObservacaoEnfermeiro { get; init; }
}

public record ColetaItemFilaDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    Guid InstituicaoId,
    string InstituicaoNome,
    StatusColeta Status,
    DateTime CriadaEmUtc,
    string? ResumoPreliminarTexto
);

// --- Agenda & Consultas ---
public record SlotAgendaDto(
    Guid Id,
    Guid MedicoId,
    string MedicoNome,
    string Especialidade,
    Guid InstituicaoId,
    string InstituicaoNome,
    DateTime InicioUtc,
    DateTime FimUtc,
    bool Disponivel
);

public record CriarSlotDto
{
    public Guid MedicoId { get; init; }
    public Guid InstituicaoId { get; init; }
    public DateTime InicioUtc { get; init; }
    public DateTime FimUtc { get; init; }
}

public record AgendarConsultaDto
{
    public Guid PacienteId { get; init; }
    public Guid SlotId { get; init; }
    public Guid ColetaId { get; init; }
}

public record ConsultaDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    Guid MedicoId,
    string MedicoNome,
    string Especialidade,
    Guid InstituicaoId,
    string InstituicaoNome,
    Guid SlotId,
    DateTime InicioUtc,
    DateTime FimUtc,
    Guid ColetaId,
    string? ResumoValidado,
    StatusConsulta Status,
    DateTime CriadaEmUtc,
    bool TemEvolucao,
    bool TemPesquisaRespondida
);

public record AtualizarStatusConsultaDto
{
    public StatusConsulta NovoStatus { get; init; }
}

// --- Evolução & Prontuário ---
public record RegistrarEvolucaoDto
{
    public string Texto { get; init; } = string.Empty;
}

public record EvolucaoDto(
    Guid Id,
    Guid ConsultaId,
    Guid MedicoId,
    string MedicoNome,
    string Texto,
    DateTime CriadaEmUtc
);

public record DocumentoPacienteDto(
    Guid Id,
    Guid PacienteId,
    Guid InstituicaoOrigemId,
    string InstituicaoOrigemNome,
    string NomeOriginal,
    string Tipo,
    string Extensao,
    long TamanhoBytes,
    DateTime CriadoEmUtc
);

public record HistoricoPacienteDto(
    Guid PacienteId,
    string Nome,
    DateTime DataNascimento,
    List<ConsultaDto> Consultas,
    List<EvolucaoDto> Evolucoes,
    List<DocumentoPacienteDto> Documentos,
    List<AutorizacaoDto> AutorizacoesAtivas
);

// --- Compartilhamento ---
public record CriarAutorizacaoDto
{
    public Guid InstituicaoOrigemId { get; init; }
    public Guid InstituicaoDestinoId { get; init; }
}

public record AutorizacaoDto(
    Guid Id,
    Guid PacienteId,
    string PacienteNome,
    Guid InstituicaoOrigemId,
    string InstituicaoOrigemNome,
    Guid InstituicaoDestinoId,
    string InstituicaoDestinoNome,
    DateTime CriadaEmUtc,
    DateTime? RevogadaEmUtc,
    bool Ativa
);

// --- Notificações & Pesquisa ---
public record NotificacaoDto(
    Guid Id,
    Guid PacienteId,
    Guid? ConsultaId,
    CanalNotificacao Canal,
    FinalidadeNotificacao Finalidade,
    string Titulo,
    string Texto,
    DateTime CriadaEmUtc,
    DateTime? LidaEmUtc
);

public record PesquisaSatisfacaoDto(
    Guid Id,
    Guid ConsultaId,
    Guid PacienteId,
    int Nota,
    PercepcaoAtendimento Percepcao,
    string? Comentario,
    DateTime RespondidaEmUtc
);

public record ResponderPesquisaDto
{
    public int Nota { get; init; }
    public PercepcaoAtendimento Percepcao { get; init; }
    public string? Comentario { get; init; }
}

// --- Dashboard ---
public record DashboardOperacionalDto(
    int TotalConsultasAgendadas,
    int TotalConsultasEmAtendimento,
    int TotalConsultasConcluidas,
    int TotalConsultasCanceladas,
    int TotalColetasPendentesValidacao,
    int TotalPesquisasRespondidas,
    double MediaSatisfacao
);

// --- Admin ---
public record CriarUsuarioAdminDto
{
    public string NomeCompleto { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
    public PerfilUsuario Perfil { get; init; }
    public Guid? InstituicaoId { get; init; }
    public string? EspecialidadeMedico { get; init; }
    public string? CrmMedico { get; init; }
}
