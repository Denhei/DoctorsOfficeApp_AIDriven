using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Mongo;

namespace Consultorio.Api.Services;

/// <summary>
/// Roteiro determinístico da triagem, sem I/O. A pergunta é indexada pela etapa em que o
/// paciente chega, então cada texto existe uma única vez; <see cref="Avancar"/> aplica a
/// resposta ao resumo e decide a próxima etapa.
/// </summary>
public static class RoteiroTriagem
{
    public const string MensagemForaDoRoteiro = "Nesta demonstração, consigo coletar informações para a equipe. Para outras dúvidas, solicite atendimento humano.";

    private const string NaoInformado = "não informado";

    public static MensagemMongo BoasVindas(Paciente paciente, Instituicao instituicao) => MensagemMongo.Bot(
        $"Olá, {paciente.Nome}! Sou o assistente virtual para coleta prévia de informações da {instituicao.Nome}.\n\nSeus dados cadastrais registrados são:\n• Nome: {paciente.Nome}\n• Nascimento: {paciente.DataNascimento:dd/MM/yyyy}\n• Telefone: {paciente.TelefoneFicticio}\n\nVocê confirma que estes dados estão corretos?",
        "Sim, confirmo meus dados", "Preciso corrigir meu cadastro");

    /// <summary>Aplica a resposta à etapa atual e devolve a próxima etapa, ou null quando o roteiro já terminou.</summary>
    public static int? Avancar(ResumoPreliminarMongo resumo, int etapaAtual, string resposta)
    {
        switch (etapaAtual)
        {
            case 1:
                return 2;

            case 2:
                resumo.Queixa = OuNaoInformado(resposta);
                return 3;

            case 3:
                resumo.Duracao = OuNaoInformado(resposta);
                return 4;

            case 4:
                if (Contem(resposta, "Sim")) return 5;
                resumo.Alergias = Contem(resposta, "Não sei") ? "Não sabe informar" : "Nenhuma relatada";
                return 6;

            case 5:
                resumo.Alergias = OuNaoInformado(resposta);
                return 6;

            case 6:
                if (Contem(resposta, "Sim")) return 7;
                resumo.Medicamentos = Contem(resposta, "Não sei") ? "Não sabe informar" : "Nenhum relatado";
                return 8;

            case 7:
                resumo.Medicamentos = OuNaoInformado(resposta);
                return 8;

            case 8:
                resumo.Observacoes = Contem(resposta, "Não tenho") ? "Nenhuma observação adicional" : resposta;
                return 9;

            default:
                return null;
        }
    }

    /// <summary>Pergunta que o bot faz ao chegar na etapa.</summary>
    public static MensagemMongo Pergunta(int etapa, string instituicaoNome, ResumoPreliminarMongo resumo) => etapa switch
    {
        2 => MensagemMongo.Bot($"Perfeito! Seu atendimento será realizado na unidade: **{instituicaoNome}**.\n\nPor favor, descreva em poucas palavras a sua **queixa principal** (o que você está sentindo):"),
        3 => MensagemMongo.Bot("Entendido. **Há quanto tempo** você está sentindo esses sintomas? (ex: 2 dias, 1 semana, hoje de manhã)"),
        4 => MensagemMongo.Bot("Você possui algum tipo de **alergia** conhecida (medicamentos, alimentos, etc.)?",
            "Não possuo alergias", "Sim, possuo alergia", "Não sei informar"),
        5 => MensagemMongo.Bot("Por favor, digite a quais substâncias ou medicamentos você tem alergia:"),
        6 => MensagemMongo.Bot("Você está fazendo **uso de algum medicamento** contínuo ou recente?",
            "Não tomo medicamentos", "Sim, tomo medicamentos", "Não sei informar"),
        7 => MensagemMongo.Bot("Por favor, digite o nome dos medicamentos e dosagem (se souber):"),
        8 => MensagemMongo.Bot("Deseja acrescentar mais alguma **informação ou observação** relevante para a equipe de enfermagem?",
            "Não tenho observações adicionais"),
        9 => MensagemMongo.Bot($"📋 **Resumo da Coleta Preliminar:**\n\n{resumo.GerarTextoFormatado()}\n\nVocê confirma as informações acima para envio à fila de triagem do enfermeiro?",
            "Confirmar e Enviar para o Enfermeiro"),
        _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, "Etapa sem pergunta no roteiro."),
    };

    private static string OuNaoInformado(string texto) => string.IsNullOrWhiteSpace(texto) ? NaoInformado : texto;

    private static bool Contem(string resposta, string trecho) => resposta.Contains(trecho, StringComparison.OrdinalIgnoreCase);
}
