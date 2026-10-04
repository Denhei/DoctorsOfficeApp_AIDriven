using Consultorio.Api.Models.Enums;

namespace Consultorio.Api.DTOs;

// --- Instituição & Médico ---
public record InstituicaoDto(Guid Id, string Nome, string RazaoSocial, bool Ativa);

public record MedicoDto(Guid Id, Guid UsuarioId, string Nome, string Especialidade, string CrmFicticio, Guid InstituicaoId, string InstituicaoNome);

// --- Paciente ---
public record PacienteDto(Guid Id, Guid UsuarioId, string Nome, DateTime DataNascimento, string TelefoneFicticio, string EmailFicticio, DateTime CriadoEmUtc);

public class CriarPacienteDto
{
    public string Nome { get; set; } = string.Empty;
    public DateTime DataNascimento { get; set; }
    public string TelefoneFicticio { get; set; } = string.Empty;
    public string EmailFicticio { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
}

public class AtualizarPacienteDto
{
    public string? TelefoneFicticio { get; set; }
    public string? EmailFicticio { get; set; }
}

// --- Coleta & Chat ---
public class IniciarColetaDto
{
    public Guid InstituicaoId { get; set; }
}

public class EnviarMensagemChatDto
{
    public string Conteudo { get; set; } = string.Empty;
    public string? ClienteMessageId { get; set; }
    public int? EtapaEsperada { get; set; }
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

public class ValidarColetaDto
{
    public bool AprovadoParaAgendamento { get; set; } // true -> Validada, false -> EncaminhadaPresencial
    public string ResumoCorrigido { get; set; } = string.Empty;
    public string? ObservacaoEnfermeiro { get; set; }
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

public class CriarSlotDto
{
    public Guid MedicoId { get; set; }
    public Guid InstituicaoId { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FimUtc { get; set; }
}

public class AgendarConsultaDto
{
    public Guid PacienteId { get; set; }
    public Guid SlotId { get; set; }
    public Guid ColetaId { get; set; }
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

public class AtualizarStatusConsultaDto
{
    public StatusConsulta NovoStatus { get; set; }
}

// --- Evolução & Prontuário ---
public class RegistrarEvolucaoDto
{
    public string Texto { get; set; } = string.Empty;
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
public class CriarAutorizacaoDto
{
    public Guid InstituicaoOrigemId { get; set; }
    public Guid InstituicaoDestinoId { get; set; }
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

public class ResponderPesquisaDto
{
    public int Nota { get; set; }
    public PercepcaoAtendimento Percepcao { get; set; }
    public string? Comentario { get; set; }
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
public class CriarUsuarioAdminDto
{
    public string NomeCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; }
    public Guid? InstituicaoId { get; set; }
    public string? EspecialidadeMedico { get; set; }
    public string? CrmMedico { get; set; }
}
