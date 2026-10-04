namespace Consultorio.Api.Models.Enums;

public enum PerfilUsuario
{
    Paciente = 1,
    Atendente = 2,
    Enfermeiro = 3,
    Medico = 4,
    Admin = 5
}

public enum StatusColeta
{
    EmColeta = 1,
    PendenteValidacao = 2,
    Validada = 3,
    EncaminhadaPresencial = 4
}

public enum StatusConsulta
{
    Agendada = 1,
    EmAtendimento = 2,
    Concluida = 3,
    Cancelada = 4
}

public enum PercepcaoAtendimento
{
    Melhor = 1,
    Igual = 2,
    Pior = 3,
    NaoInformado = 4
}

public enum CanalNotificacao
{
    WhatsApp = 1,
    Email = 2
}

public enum FinalidadeNotificacao
{
    ConvitePesquisa = 1,
    LembreteConsulta = 2,
    StatusColeta = 3
}
