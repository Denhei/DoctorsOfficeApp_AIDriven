using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Models.Mongo;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace Consultorio.Api.Services;

public interface ITriagemChatService
{
    Task<ConversaDetalheDto> IniciarColetaAsync(Guid pacienteId, Guid instituicaoId);
    Task<ConversaDetalheDto?> ObterConversaPorColetaIdAsync(Guid coletaId, UsuarioAtual solicitante);
    Task<ConversaDetalheDto> ProcessarMensagemAsync(Guid coletaId, UsuarioAtual solicitante, EnviarMensagemChatDto dto);
    Task<ConversaDetalheDto> FinalizarEnvioEnfermeiroAsync(Guid coletaId, UsuarioAtual solicitante);
    Task<List<ColetaItemFilaDto>> ListarFilaEnfermeiroAsync(Guid enfermeiroInstituicaoId);
    Task<bool> ValidarColetaAsync(Guid coletaId, UsuarioAtual enfermeiro, ValidarColetaDto dto);
}

public class TriagemChatService(AppDbContext context, MongoDbContext mongo) : ITriagemChatService
{
    public async Task<ConversaDetalheDto> IniciarColetaAsync(Guid pacienteId, Guid instituicaoId)
    {
        var paciente = await context.Pacientes.FindAsync(pacienteId)
            ?? throw new RegraDeNegocioException("Paciente não encontrado.");

        var instituicao = await context.Instituicoes.FindAsync(instituicaoId)
            ?? throw new RegraDeNegocioException("Instituição não encontrada.");

        var coleta = new ColetaInicial
        {
            PacienteId = pacienteId,
            Paciente = paciente,
            InstituicaoId = instituicaoId,
            Instituicao = instituicao,
            Status = StatusColeta.EmColeta,
            ConversaId = Guid.NewGuid(),
            CriadaEmUtc = DateTime.UtcNow
        };

        context.ColetasIniciais.Add(coleta);
        await context.SaveChangesAsync();

        var conversa = new ConversaMongo
        {
            Id = coleta.ConversaId.ToString(),
            ColetaId = coleta.Id.ToString(),
            PacienteId = pacienteId.ToString(),
            InstituicaoId = instituicaoId.ToString(),
            EtapaAtual = 1,
            Mensagens = [RoteiroTriagem.BoasVindas(paciente, instituicao)],
        };

        await mongo.Conversas.InsertOneAsync(conversa);

        return coleta.ToDetalheDto(conversa);
    }

    public async Task<ConversaDetalheDto?> ObterConversaPorColetaIdAsync(Guid coletaId, UsuarioAtual solicitante)
    {
        var coleta = await context.ColetasIniciais
            .Include(c => c.Paciente)
            .Include(c => c.Instituicao)
            .FirstOrDefaultAsync(c => c.Id == coletaId);

        if (coleta == null) return null;

        if (solicitante.Perfil == PerfilUsuario.Paciente && coleta.Paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Acesso negado aos dados de outro paciente.");
        }

        var conversa = await ConversaAsync(coleta.ConversaId);
        if (conversa == null) return null;

        return coleta.ToDetalheDto(conversa);
    }

    public async Task<ConversaDetalheDto> ProcessarMensagemAsync(Guid coletaId, UsuarioAtual solicitante, EnviarMensagemChatDto dto)
    {
        var coleta = await ColetaDoPacienteAsync(coletaId, solicitante.Id);

        if (coleta.Status != StatusColeta.EmColeta)
        {
            throw new RegraDeNegocioException("Esta coleta já foi enviada para triagem e não pode receber novas respostas.");
        }

        var conversa = await ConversaAsync(coleta.ConversaId)
            ?? throw new RegraDeNegocioException("Conversa não encontrada no MongoDB.");

        // Reenvio do mesmo ClienteMessageId devolve o estado atual sem reprocessar.
        if (!string.IsNullOrEmpty(dto.ClienteMessageId) && conversa.Mensagens.Any(m => m.ClienteMessageId == dto.ClienteMessageId))
        {
            return coleta.ToDetalheDto(conversa);
        }

        var resposta = dto.Conteudo.Trim();
        conversa.Mensagens.Add(MensagemMongo.DoPaciente(resposta, dto.ClienteMessageId));

        var proximaEtapa = RoteiroTriagem.Avancar(conversa.ResumoPreliminar, conversa.EtapaAtual, resposta);
        if (proximaEtapa is null)
        {
            conversa.Mensagens.Add(MensagemMongo.Bot(RoteiroTriagem.MensagemForaDoRoteiro));
        }
        else
        {
            conversa.EtapaAtual = proximaEtapa.Value;
            conversa.Mensagens.Add(RoteiroTriagem.Pergunta(proximaEtapa.Value, coleta.Instituicao.Nome, conversa.ResumoPreliminar));
        }

        await SalvarConversaAsync(conversa);

        return coleta.ToDetalheDto(conversa);
    }

    public async Task<ConversaDetalheDto> FinalizarEnvioEnfermeiroAsync(Guid coletaId, UsuarioAtual solicitante)
    {
        var coleta = await ColetaDoPacienteAsync(coletaId, solicitante.Id);

        var conversa = await ConversaAsync(coleta.ConversaId)
            ?? throw new RegraDeNegocioException("Conversa não encontrada.");

        coleta.Status = StatusColeta.PendenteValidacao;
        await context.SaveChangesAsync();

        conversa.Mensagens.Add(MensagemMongo.Bot("✅ Sua coleta foi enviada com sucesso! Um enfermeiro da nossa equipe irá revisar seus dados para liberar a escolha de horários na agenda."));
        await SalvarConversaAsync(conversa);

        return coleta.ToDetalheDto(conversa);
    }

    public async Task<List<ColetaItemFilaDto>> ListarFilaEnfermeiroAsync(Guid enfermeiroInstituicaoId)
    {
        var coletas = await context.ColetasIniciais
            .Include(c => c.Paciente)
            .Include(c => c.Instituicao)
            .Where(c => c.InstituicaoId == enfermeiroInstituicaoId && c.Status == StatusColeta.PendenteValidacao)
            .OrderBy(c => c.CriadaEmUtc)
            .ToListAsync();

        var ids = coletas.Select(c => c.ConversaId.ToString()).ToList();
        var conversas = (await mongo.Conversas.Find(c => ids.Contains(c.Id)).ToListAsync())
            .ToDictionary(c => c.Id);

        return coletas.Select(c => c.ToFilaDto(conversas.GetValueOrDefault(c.ConversaId.ToString()))).ToList();
    }

    public async Task<bool> ValidarColetaAsync(Guid coletaId, UsuarioAtual enfermeiro, ValidarColetaDto dto)
    {
        var coleta = await context.ColetasIniciais.FindAsync(coletaId);

        if (coleta == null || coleta.Status != StatusColeta.PendenteValidacao)
        {
            return false;
        }

        coleta.EnfermeiroId = enfermeiro.Id;
        coleta.ValidadoEmUtc = DateTime.UtcNow;
        coleta.ResumoValidado = dto.ResumoCorrigido;
        coleta.Status = dto.AprovadoParaAgendamento ? StatusColeta.Validada : StatusColeta.EncaminhadaPresencial;

        await context.SaveChangesAsync();

        var conversa = await ConversaAsync(coleta.ConversaId);
        if (conversa != null)
        {
            conversa.Mensagens.Add(MensagemMongo.Bot(dto.AprovadoParaAgendamento
                ? $"🩺 **Validação pela Enfermagem Concluída:**\nSua coleta preliminar foi revisada e aprovada pelo enfermeiro.\n\n*Resumo Validado:* {dto.ResumoCorrigido}\n\nVocê já pode prosseguir para o **agendamento da consulta**!"
                : $"⚠️ **Orientação da Enfermagem:**\nCom base nas informações relatadas, recomendamos que você compareça diretamente para **avaliação presencial imediata** na unidade de emergência.\nObservação: {dto.ObservacaoEnfermeiro}"));
            await SalvarConversaAsync(conversa);
        }

        return true;
    }

    private async Task<ColetaInicial> ColetaDoPacienteAsync(Guid coletaId, Guid pacienteUsuarioId)
    {
        var coleta = await context.ColetasIniciais
            .Include(c => c.Paciente)
            .Include(c => c.Instituicao)
            .FirstOrDefaultAsync(c => c.Id == coletaId)
            ?? throw new RegraDeNegocioException("Coleta não encontrada.");

        if (coleta.Paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new AcessoNegadoException("Acesso negado.");
        }

        return coleta;
    }

    private async Task<ConversaMongo?> ConversaAsync(Guid conversaId)
    {
        var id = conversaId.ToString();
        return await mongo.Conversas.Find(c => c.Id == id).FirstOrDefaultAsync();
    }

    private Task SalvarConversaAsync(ConversaMongo conversa)
    {
        conversa.AtualizadaEmUtc = DateTime.UtcNow;
        return mongo.Conversas.ReplaceOneAsync(c => c.Id == conversa.Id, conversa);
    }

}
