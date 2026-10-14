using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Models.Mongo;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace Consultorio.Api.Services;

public interface ITriagemChatService
{
    Task<ConversaDetalheDto> IniciarColetaAsync(Guid pacienteId, Guid instituicaoId);
    Task<ConversaDetalheDto?> ObterConversaPorColetaIdAsync(Guid coletaId, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<ConversaDetalheDto> ProcessarMensagemAsync(Guid coletaId, Guid pacienteUsuarioId, EnviarMensagemChatDto dto);
    Task<ConversaDetalheDto> FinalizarEnvioEnfermeiroAsync(Guid coletaId, Guid pacienteUsuarioId);
    Task<List<ColetaItemFilaDto>> ListarFilaEnfermeiroAsync(Guid enfermeiroInstituicaoId);
    Task<bool> ValidarColetaAsync(Guid coletaId, Guid enfermeiroUsuarioId, ValidarColetaDto dto);
}

public class TriagemChatService : ITriagemChatService
{
    private readonly AppDbContext _context;
    private readonly MongoDbContext _mongoContext;
    private const string MENSAGEM_PADRAO_FORA_ROTEIRO = "Nesta demonstração, consigo coletar informações para a equipe. Para outras dúvidas, solicite atendimento humano.";

    public TriagemChatService(AppDbContext context, MongoDbContext mongoContext)
    {
        _context = context;
        _mongoContext = mongoContext;
    }

    public async Task<ConversaDetalheDto> IniciarColetaAsync(Guid pacienteId, Guid instituicaoId)
    {
        var paciente = await _context.Pacientes.FindAsync(pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        var instituicao = await _context.Instituicoes.FindAsync(instituicaoId)
            ?? throw new InvalidOperationException("Instituição não encontrada.");

        var conversaId = Guid.NewGuid();

        // 1. Cria Coleta no PostgreSQL
        var coleta = new ColetaInicial
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId,
            InstituicaoId = instituicaoId,
            Status = StatusColeta.EmColeta,
            ConversaId = conversaId,
            CriadaEmUtc = DateTime.UtcNow
        };

        await _context.ColetasIniciais.AddAsync(coleta);
        await _context.SaveChangesAsync();

        // 2. Inicializa documento no MongoDB de forma idempotente
        var conversaMongo = new ConversaMongo
        {
            Id = conversaId.ToString(),
            ColetaId = coleta.Id.ToString(),
            PacienteId = pacienteId.ToString(),
            InstituicaoId = instituicaoId.ToString(),
            EtapaAtual = 1,
            Status = "EmAndamento",
            ResumoPreliminar = new ResumoPreliminarMongo(),
            Mensagens = new List<MensagemMongo>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = $"Olá, {paciente.Nome}! Sou o assistente virtual para coleta prévia de informações da {instituicao.Nome}.\n\nSeus dados cadastrais registrados são:\n• Nome: {paciente.Nome}\n• Nascimento: {paciente.DataNascimento:dd/MM/yyyy}\n• Telefone: {paciente.TelefoneFicticio}\n\nVocê confirma que estes dados estão corretos?",
                    Tipo = "Opcoes",
                    Opcoes = new List<string> { "Sim, confirmo meus dados", "Preciso corrigir meu cadastro" },
                    EnviadaEmUtc = DateTime.UtcNow
                }
            },
            CriadaEmUtc = DateTime.UtcNow,
            AtualizadaEmUtc = DateTime.UtcNow
        };

        await _mongoContext.Conversas.InsertOneAsync(conversaMongo);

        return MapearDetalheDto(coleta, paciente, instituicao, conversaMongo);
    }

    public async Task<ConversaDetalheDto?> ObterConversaPorColetaIdAsync(Guid coletaId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        var coleta = await _context.ColetasIniciais
            .Include(c => c.Paciente)
            .Include(c => c.Instituicao)
            .FirstOrDefaultAsync(c => c.Id == coletaId);

        if (coleta == null) return null;

        // Validação de acesso RBAC
        if (perfil == PerfilUsuario.Paciente && coleta.Paciente.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado aos dados de outro paciente.");
        }

        var conversa = await _mongoContext.Conversas
            .Find(c => c.Id == coleta.ConversaId.ToString())
            .FirstOrDefaultAsync();

        if (conversa == null) return null;

        return MapearDetalheDto(coleta, coleta.Paciente, coleta.Instituicao, conversa);
    }

    public async Task<ConversaDetalheDto> ProcessarMensagemAsync(Guid coletaId, Guid pacienteUsuarioId, EnviarMensagemChatDto dto)
    {
        var coleta = await _context.ColetasIniciais
            .Include(c => c.Paciente)
            .Include(c => c.Instituicao)
            .FirstOrDefaultAsync(c => c.Id == coletaId)
            ?? throw new InvalidOperationException("Coleta não encontrada.");

        if (coleta.Paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        if (coleta.Status != StatusColeta.EmColeta)
        {
            throw new InvalidOperationException("Esta coleta já foi enviada para triagem e não pode receber novas respostas.");
        }

        var conversa = await _mongoContext.Conversas
            .Find(c => c.Id == coleta.ConversaId.ToString())
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Conversa não encontrada no MongoDB.");

        // Idempotência: verificar se a mensagem do cliente já foi registrada
        if (!string.IsNullOrEmpty(dto.ClienteMessageId) &&
            conversa.Mensagens.Any(m => m.ClienteMessageId == dto.ClienteMessageId))
        {
            return MapearDetalheDto(coleta, coleta.Paciente, coleta.Instituicao, conversa);
        }

        var textoLimpo = dto.Conteudo.Trim();

        // Registrar mensagem do paciente
        conversa.Mensagens.Add(new MensagemMongo
        {
            Id = Guid.NewGuid().ToString(),
            Remetente = "Paciente",
            Conteudo = textoLimpo,
            Tipo = "Texto",
            ClienteMessageId = dto.ClienteMessageId,
            EnviadaEmUtc = DateTime.UtcNow
        });

        // Máquina de Estados Determinística
        switch (conversa.EtapaAtual)
        {
            case 1: // Confirmação de dados
                conversa.EtapaAtual = 2;
                conversa.Mensagens.Add(new MensagemMongo
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = $"Perfeito! Seu atendimento será realizado na unidade: **{coleta.Instituicao.Nome}**.\n\nPor favor, descreva em poucas palavras a sua **queixa principal** (o que você está sentindo):",
                    Tipo = "Texto",
                    EnviadaEmUtc = DateTime.UtcNow
                });
                break;

            case 2: // Queixa principal
                conversa.ResumoPreliminar.Queixa = string.IsNullOrWhiteSpace(textoLimpo) ? "não informado" : textoLimpo;
                conversa.EtapaAtual = 3;
                conversa.Mensagens.Add(new MensagemMongo
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = "Entendido. **Há quanto tempo** você está sentindo esses sintomas? (ex: 2 dias, 1 semana, hoje de manhã)",
                    Tipo = "Texto",
                    EnviadaEmUtc = DateTime.UtcNow
                });
                break;

            case 3: // Duração
                conversa.ResumoPreliminar.Duracao = string.IsNullOrWhiteSpace(textoLimpo) ? "não informado" : textoLimpo;
                conversa.EtapaAtual = 4;
                conversa.Mensagens.Add(new MensagemMongo
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = "Você possui algum tipo de **alergia** conhecida (medicamentos, alimentos, etc.)?",
                    Tipo = "Opcoes",
                    Opcoes = new List<string> { "Não possuo alergias", "Sim, possuo alergia", "Não sei informar" },
                    EnviadaEmUtc = DateTime.UtcNow
                });
                break;

            case 4: // Alergias
                if (textoLimpo.Contains("Sim", StringComparison.OrdinalIgnoreCase))
                {
                    conversa.EtapaAtual = 5; // Sub-etapa para detalhar alergia
                    conversa.Mensagens.Add(new MensagemMongo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Remetente = "Bot",
                        Conteudo = "Por favor, digite a quais substâncias ou medicamentos você tem alergia:",
                        Tipo = "Texto",
                        EnviadaEmUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    conversa.ResumoPreliminar.Alergias = textoLimpo.Contains("Não sei", StringComparison.OrdinalIgnoreCase) 
                        ? "Não sabe informar" 
                        : "Nenhuma relatada";
                    conversa.EtapaAtual = 6;
                    conversa.Mensagens.Add(new MensagemMongo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Remetente = "Bot",
                        Conteudo = "Você está fazendo **uso de algum medicamento** contínuo ou recente?",
                        Tipo = "Opcoes",
                        Opcoes = new List<string> { "Não tomo medicamentos", "Sim, tomo medicamentos", "Não sei informar" },
                        EnviadaEmUtc = DateTime.UtcNow
                    });
                }
                break;

            case 5: // Detalhe da alergia
                conversa.ResumoPreliminar.Alergias = string.IsNullOrWhiteSpace(textoLimpo) ? "não informado" : textoLimpo;
                conversa.EtapaAtual = 6;
                conversa.Mensagens.Add(new MensagemMongo
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = "Você está fazendo **uso de algum medicamento** contínuo ou recente?",
                    Tipo = "Opcoes",
                    Opcoes = new List<string> { "Não tomo medicamentos", "Sim, tomo medicamentos", "Não sei informar" },
                    EnviadaEmUtc = DateTime.UtcNow
                });
                break;

            case 6: // Medicamentos
                if (textoLimpo.Contains("Sim", StringComparison.OrdinalIgnoreCase))
                {
                    conversa.EtapaAtual = 7; // Sub-etapa para detalhar medicamento
                    conversa.Mensagens.Add(new MensagemMongo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Remetente = "Bot",
                        Conteudo = "Por favor, digite o nome dos medicamentos e dosagem (se souber):",
                        Tipo = "Texto",
                        EnviadaEmUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    conversa.ResumoPreliminar.Medicamentos = textoLimpo.Contains("Não sei", StringComparison.OrdinalIgnoreCase) 
                        ? "Não sabe informar" 
                        : "Nenhum relatado";
                    conversa.EtapaAtual = 8;
                    conversa.Mensagens.Add(new MensagemMongo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Remetente = "Bot",
                        Conteudo = "Deseja acrescentar mais alguma **informação ou observação** relevante para a equipe de enfermagem?",
                        Tipo = "Opcoes",
                        Opcoes = new List<string> { "Não tenho observações adicionais" },
                        EnviadaEmUtc = DateTime.UtcNow
                    });
                }
                break;

            case 7: // Detalhe dos medicamentos
                conversa.ResumoPreliminar.Medicamentos = string.IsNullOrWhiteSpace(textoLimpo) ? "não informado" : textoLimpo;
                conversa.EtapaAtual = 8;
                conversa.Mensagens.Add(new MensagemMongo
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = "Deseja acrescentar mais alguma **informação ou observação** relevante para a equipe de enfermagem?",
                    Tipo = "Opcoes",
                    Opcoes = new List<string> { "Não tenho observações adicionais" },
                    EnviadaEmUtc = DateTime.UtcNow
                });
                break;

            case 8: // Observações e Geração do Resumo
                if (!textoLimpo.Contains("Não tenho", StringComparison.OrdinalIgnoreCase))
                {
                    conversa.ResumoPreliminar.Observacoes = textoLimpo;
                }
                else
                {
                    conversa.ResumoPreliminar.Observacoes = "Nenhuma observação adicional";
                }

                conversa.EtapaAtual = 9; // Pronto para conferência
                var textoResumo = conversa.ResumoPreliminar.GerarTextoFormatado();

                conversa.Mensagens.Add(new MensagemMongo
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = $"📋 **Resumo da Coleta Preliminar:**\n\n{textoResumo}\n\nVocê confirma as informações acima para envio à fila de triagem do enfermeiro?",
                    Tipo = "Opcoes",
                    Opcoes = new List<string> { "Confirmar e Enviar para o Enfermeiro" },
                    EnviadaEmUtc = DateTime.UtcNow
                });
                break;

            default:
                // Entrada fora de roteiro
                conversa.Mensagens.Add(new MensagemMongo
                {
                    Id = Guid.NewGuid().ToString(),
                    Remetente = "Bot",
                    Conteudo = MENSAGEM_PADRAO_FORA_ROTEIRO,
                    Tipo = "Texto",
                    EnviadaEmUtc = DateTime.UtcNow
                });
                break;
        }

        conversa.AtualizadaEmUtc = DateTime.UtcNow;

        await _mongoContext.Conversas.ReplaceOneAsync(
            c => c.Id == conversa.Id,
            conversa
        );

        return MapearDetalheDto(coleta, coleta.Paciente, coleta.Instituicao, conversa);
    }

    public async Task<ConversaDetalheDto> FinalizarEnvioEnfermeiroAsync(Guid coletaId, Guid pacienteUsuarioId)
    {
        var coleta = await _context.ColetasIniciais
            .Include(c => c.Paciente)
            .Include(c => c.Instituicao)
            .FirstOrDefaultAsync(c => c.Id == coletaId)
            ?? throw new InvalidOperationException("Coleta não encontrada.");

        if (coleta.Paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        var conversa = await _mongoContext.Conversas
            .Find(c => c.Id == coleta.ConversaId.ToString())
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Conversa não encontrada.");

        // Atualiza status no PostgreSQL
        coleta.Status = StatusColeta.PendenteValidacao;
        await _context.SaveChangesAsync();

        // Atualiza status no MongoDB
        conversa.Status = "PendenteValidacao";
        conversa.Mensagens.Add(new MensagemMongo
        {
            Id = Guid.NewGuid().ToString(),
            Remetente = "Bot",
            Conteudo = "✅ Sua coleta foi enviada com sucesso! Um enfermeiro da nossa equipe irá revisar seus dados para liberar a escolha de horários na agenda.",
            Tipo = "Texto",
            EnviadaEmUtc = DateTime.UtcNow
        });
        conversa.AtualizadaEmUtc = DateTime.UtcNow;

        await _mongoContext.Conversas.ReplaceOneAsync(c => c.Id == conversa.Id, conversa);

        return MapearDetalheDto(coleta, coleta.Paciente, coleta.Instituicao, conversa);
    }

    public async Task<List<ColetaItemFilaDto>> ListarFilaEnfermeiroAsync(Guid enfermeiroInstituicaoId)
    {
        var coletas = await _context.ColetasIniciais
            .Include(c => c.Paciente)
            .Include(c => c.Instituicao)
            .Where(c => c.InstituicaoId == enfermeiroInstituicaoId && c.Status == StatusColeta.PendenteValidacao)
            .OrderBy(c => c.CriadaEmUtc)
            .ToListAsync();

        var lista = new List<ColetaItemFilaDto>();
        foreach (var c in coletas)
        {
            var conversa = await _mongoContext.Conversas
                .Find(conv => conv.Id == c.ConversaId.ToString())
                .FirstOrDefaultAsync();

            lista.Add(new ColetaItemFilaDto(
                c.Id,
                c.PacienteId,
                c.Paciente.Nome,
                c.InstituicaoId,
                c.Instituicao.Nome,
                c.Status,
                c.CriadaEmUtc,
                conversa?.ResumoPreliminar?.GerarTextoFormatado()
            ));
        }

        return lista;
    }

    public async Task<bool> ValidarColetaAsync(Guid coletaId, Guid enfermeiroUsuarioId, ValidarColetaDto dto)
    {
        var coleta = await _context.ColetasIniciais
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == coletaId);

        if (coleta == null || coleta.Status != StatusColeta.PendenteValidacao)
        {
            return false;
        }

        coleta.EnfermeiroId = enfermeiroUsuarioId;
        coleta.ValidadoEmUtc = DateTime.UtcNow;
        coleta.ResumoValidado = dto.ResumoCorrigido;

        if (dto.AprovadoParaAgendamento)
        {
            coleta.Status = StatusColeta.Validada;
        }
        else
        {
            coleta.Status = StatusColeta.EncaminhadaPresencial;
        }

        await _context.SaveChangesAsync();

        // Notificar no chat do MongoDB
        var conversa = await _mongoContext.Conversas
            .Find(c => c.Id == coleta.ConversaId.ToString())
            .FirstOrDefaultAsync();

        if (conversa != null)
        {
            conversa.Status = coleta.Status.ToString();
            var msg = dto.AprovadoParaAgendamento
                ? $"🩺 **Validação pela Enfermagem Concluída:**\nSua coleta preliminar foi revisada e aprovada pelo enfermeiro.\n\n*Resumo Validado:* {dto.ResumoCorrigido}\n\nVocê já pode prosseguir para o **agendamento da consulta**!"
                : $"⚠️ **Orientação da Enfermagem:**\nCom base nas informações relatadas, recomendamos que você compareça diretamente para **avaliação presencial imediata** na unidade de emergência.\nObservação: {dto.ObservacaoEnfermeiro}";

            conversa.Mensagens.Add(new MensagemMongo
            {
                Id = Guid.NewGuid().ToString(),
                Remetente = "Bot",
                Conteudo = msg,
                Tipo = "Texto",
                EnviadaEmUtc = DateTime.UtcNow
            });
            conversa.AtualizadaEmUtc = DateTime.UtcNow;

            await _mongoContext.Conversas.ReplaceOneAsync(c => c.Id == conversa.Id, conversa);
        }

        return true;
    }

    private static ConversaDetalheDto MapearDetalheDto(ColetaInicial coleta, Paciente paciente, Instituicao instituicao, ConversaMongo conversa)
    {
        var mensagensDto = conversa.Mensagens.Select(m => new MensagemChatDto(
            m.Id,
            m.Remetente,
            m.Conteudo,
            m.Tipo,
            m.Opcoes,
            m.EnviadaEmUtc
        )).ToList();

        var resumoDto = new ResumoPreliminarDto(
            conversa.ResumoPreliminar.Queixa,
            conversa.ResumoPreliminar.Duracao,
            conversa.ResumoPreliminar.Alergias,
            conversa.ResumoPreliminar.Medicamentos,
            conversa.ResumoPreliminar.Observacoes
        );

        return new ConversaDetalheDto(
            coleta.Id,
            conversa.Id,
            paciente.Id,
            paciente.Nome,
            instituicao.Id,
            instituicao.Nome,
            coleta.Status,
            conversa.EtapaAtual,
            resumoDto,
            coleta.ResumoValidado,
            mensagensDto,
            conversa.CriadaEmUtc
        );
    }
}
