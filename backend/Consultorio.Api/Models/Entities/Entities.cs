using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace Consultorio.Api.Models.Entities;

public class Usuario : IdentityUser<Guid>
{
    [Required]
    [MaxLength(150)]
    public string NomeCompleto { get; set; } = string.Empty;

    public PerfilUsuario Perfil { get; set; }

    public Guid? InstituicaoId { get; set; }

    [ForeignKey(nameof(InstituicaoId))]
    public Instituicao? Instituicao { get; set; }

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
}

public class Papel : IdentityRole<Guid>
{
    public Papel() : base() { }
    public Papel(string roleName) : base(roleName) { }
}

public class Instituicao
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(200)]
    public string RazaoSocial { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Medico> Medicos { get; set; } = new List<Medico>();
    public ICollection<SlotAgenda> Slots { get; set; } = new List<SlotAgenda>();
}

public class Paciente
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public Usuario Usuario { get; set; } = null!;

    [Required]
    [MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    public DateTime DataNascimento { get; set; }

    [MaxLength(30)]
    public string TelefoneFicticio { get; set; } = string.Empty;

    [MaxLength(100)]
    public string EmailFicticio { get; set; } = string.Empty;

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ColetaInicial> Coletas { get; set; } = new List<ColetaInicial>();
    public ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
    public ICollection<DocumentoPaciente> Documentos { get; set; } = new List<DocumentoPaciente>();
    public ICollection<AutorizacaoCompartilhamento> Autorizacoes { get; set; } = new List<AutorizacaoCompartilhamento>();
}

public class Medico
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }

    [ForeignKey(nameof(UsuarioId))]
    public Usuario Usuario { get; set; } = null!;

    public Guid InstituicaoId { get; set; }

    [ForeignKey(nameof(InstituicaoId))]
    public Instituicao Instituicao { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Especialidade { get; set; } = string.Empty;

    [MaxLength(30)]
    public string CrmFicticio { get; set; } = string.Empty;

    public ICollection<SlotAgenda> Slots { get; set; } = new List<SlotAgenda>();
    public ICollection<Evolucao> Evolucoes { get; set; } = new List<Evolucao>();
}

public class SlotAgenda
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MedicoId { get; set; }

    [ForeignKey(nameof(MedicoId))]
    public Medico Medico { get; set; } = null!;

    public Guid InstituicaoId { get; set; }

    [ForeignKey(nameof(InstituicaoId))]
    public Instituicao Instituicao { get; set; } = null!;

    public DateTime InicioUtc { get; set; }

    public DateTime FimUtc { get; set; }

    public ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
}

public class ColetaInicial
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PacienteId { get; set; }

    [ForeignKey(nameof(PacienteId))]
    public Paciente Paciente { get; set; } = null!;

    public Guid InstituicaoId { get; set; }

    [ForeignKey(nameof(InstituicaoId))]
    public Instituicao Instituicao { get; set; } = null!;

    public StatusColeta Status { get; set; } = StatusColeta.EmColeta;

    public Guid ConversaId { get; set; }

    public string? ResumoValidado { get; set; }

    public Guid? EnfermeiroId { get; set; }

    [ForeignKey(nameof(EnfermeiroId))]
    public Usuario? Enfermeiro { get; set; }

    public DateTime? ValidadoEmUtc { get; set; }

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
}

public class Consulta
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PacienteId { get; set; }

    [ForeignKey(nameof(PacienteId))]
    public Paciente Paciente { get; set; } = null!;

    public Guid SlotId { get; set; }

    [ForeignKey(nameof(SlotId))]
    public SlotAgenda Slot { get; set; } = null!;

    public Guid ColetaId { get; set; }

    [ForeignKey(nameof(ColetaId))]
    public ColetaInicial Coleta { get; set; } = null!;

    public StatusConsulta Status { get; set; } = StatusConsulta.Agendada;

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;

    public Evolucao? Evolucao { get; set; }

    public PesquisaSatisfacao? PesquisaSatisfacao { get; set; }

    public ICollection<NotificacaoSimulada> Notificacoes { get; set; } = new List<NotificacaoSimulada>();
}

public class Evolucao
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConsultaId { get; set; }

    [ForeignKey(nameof(ConsultaId))]
    public Consulta Consulta { get; set; } = null!;

    public Guid MedicoId { get; set; }

    [ForeignKey(nameof(MedicoId))]
    public Medico Medico { get; set; } = null!;

    [Required]
    public string Texto { get; set; } = string.Empty;

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;
}

public class DocumentoPaciente
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PacienteId { get; set; }

    [ForeignKey(nameof(PacienteId))]
    public Paciente Paciente { get; set; } = null!;

    public Guid InstituicaoOrigemId { get; set; }

    [ForeignKey(nameof(InstituicaoOrigemId))]
    public Instituicao InstituicaoOrigem { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string NomeOriginal { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Tipo { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Extensao { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    [Required]
    [MaxLength(255)]
    public string ChaveArquivo { get; set; } = string.Empty;

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
}

public class AutorizacaoCompartilhamento
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PacienteId { get; set; }

    [ForeignKey(nameof(PacienteId))]
    public Paciente Paciente { get; set; } = null!;

    public Guid InstituicaoOrigemId { get; set; }

    [ForeignKey(nameof(InstituicaoOrigemId))]
    public Instituicao InstituicaoOrigem { get; set; } = null!;

    public Guid InstituicaoDestinoId { get; set; }

    [ForeignKey(nameof(InstituicaoDestinoId))]
    public Instituicao InstituicaoDestino { get; set; } = null!;

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RevogadaEmUtc { get; set; }
}

public class PesquisaSatisfacao
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConsultaId { get; set; }

    [ForeignKey(nameof(ConsultaId))]
    public Consulta Consulta { get; set; } = null!;

    public Guid PacienteId { get; set; }

    [ForeignKey(nameof(PacienteId))]
    public Paciente Paciente { get; set; } = null!;

    public int Nota { get; set; } // 1 a 5

    public PercepcaoAtendimento Percepcao { get; set; } = PercepcaoAtendimento.NaoInformado;

    [MaxLength(1000)]
    public string? Comentario { get; set; }

    public DateTime RespondidaEmUtc { get; set; } = DateTime.UtcNow;
}

public class NotificacaoSimulada
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PacienteId { get; set; }

    [ForeignKey(nameof(PacienteId))]
    public Paciente Paciente { get; set; } = null!;

    public Guid? ConsultaId { get; set; }

    [ForeignKey(nameof(ConsultaId))]
    public Consulta? Consulta { get; set; }

    public CanalNotificacao Canal { get; set; }

    public FinalidadeNotificacao Finalidade { get; set; }

    [Required]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    public string Texto { get; set; } = string.Empty;

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LidaEmUtc { get; set; }
}
