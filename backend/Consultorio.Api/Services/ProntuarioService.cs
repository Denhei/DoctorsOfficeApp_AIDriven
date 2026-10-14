using System.Text;
using Consultorio.Api.Data;
using Consultorio.Api.Dominio;
using Consultorio.Api.Infraestrutura;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IProntuarioService
{
    Task<EvolucaoDto> RegistrarEvolucaoAsync(Guid consultaId, UsuarioAtual medico, RegistrarEvolucaoDto dto);
    Task<ConsultaDto> ConcluirConsultaAsync(Guid consultaId, UsuarioAtual medico);
    Task<HistoricoPacienteDto> ObterHistoricoPacienteAsync(Guid pacienteId, UsuarioAtual solicitante);
    Task<DocumentoPacienteDto> AnexarDocumentoAsync(Guid pacienteId, Guid instituicaoOrigemId, IFormFile arquivo, UsuarioAtual solicitante);
    Task<List<DocumentoPacienteDto>> ListarDocumentosAsync(Guid pacienteId, UsuarioAtual solicitante);
    Task<(byte[] Bytes, string ContentType, string NomeArquivo)?> ObterArquivoParaDownloadAsync(Guid documentoId, UsuarioAtual solicitante);
    Task<AutorizacaoDto> ConcederAutorizacaoAsync(Guid pacienteId, CriarAutorizacaoDto dto, UsuarioAtual solicitante);
    Task<bool> RevogarAutorizacaoAsync(Guid autorizacaoId, UsuarioAtual solicitante);
    Task<List<AutorizacaoDto>> ListarAutorizacoesAsync(Guid pacienteId, UsuarioAtual solicitante);
}

public class ProntuarioService(AppDbContext context, ArmazenamentoArquivos arquivos) : IProntuarioService
{
    private const long TamanhoMaximoBytes = 5 * 1024 * 1024;
    private static readonly string[] ExtensoesPermitidas = [".pdf", ".jpg", ".jpeg", ".png"];

    public async Task<EvolucaoDto> RegistrarEvolucaoAsync(Guid consultaId, UsuarioAtual medico, RegistrarEvolucaoDto dto)
    {
        var medicoId = medico.MedicoId ?? throw new AcessoNegadoException("Usuário não é um médico cadastrado.");

        var consulta = await context.Consultas
            .Include(c => c.Slot)
            .Include(c => c.Evolucao).ThenInclude(e => e!.Medico).ThenInclude(m => m.Usuario)
            .FirstOrDefaultAsync(c => c.Id == consultaId)
            ?? throw new RegraDeNegocioException("Consulta não encontrada.");

        if (consulta.Slot.MedicoId != medicoId)
        {
            throw new AcessoNegadoException("Apenas o médico responsável por esta consulta pode registrar a evolução.");
        }

        if (string.IsNullOrWhiteSpace(dto.Texto))
        {
            throw new RegraDeNegocioException("O texto da evolução clínica é obrigatório.");
        }

        if (consulta.Evolucao == null)
        {
            var autor = await context.Medicos.Include(m => m.Usuario).FirstAsync(m => m.Id == medicoId);
            consulta.Evolucao = new Evolucao { ConsultaId = consulta.Id, MedicoId = medicoId, Medico = autor };
            context.Evolucoes.Add(consulta.Evolucao);
        }

        consulta.Evolucao.Texto = dto.Texto;
        consulta.Evolucao.CriadaEmUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return consulta.Evolucao.ToDto();
    }

    public async Task<ConsultaDto> ConcluirConsultaAsync(Guid consultaId, UsuarioAtual medico)
    {
        var medicoId = medico.MedicoId ?? throw new AcessoNegadoException("Usuário não é um médico.");

        using var transaction = await context.Database.BeginTransactionAsync();

        var consulta = await context.Consultas.ComDetalhes().FirstOrDefaultAsync(c => c.Id == consultaId)
            ?? throw new RegraDeNegocioException("Consulta não encontrada.");

        if (consulta.Slot.MedicoId != medicoId)
        {
            throw new AcessoNegadoException("Apenas o médico responsável pode concluir o atendimento.");
        }

        if (consulta.Evolucao == null || string.IsNullOrWhiteSpace(consulta.Evolucao.Texto))
        {
            throw new RegraDeNegocioException("É obrigatório registrar a evolução médica antes de concluir o atendimento.");
        }

        if (consulta.Status == StatusConsulta.Concluida)
        {
            return consulta.ToDto();
        }

        consulta.Status = StatusConsulta.Concluida;

        // Convite de pesquisa gerado uma única vez, na mesma transação da conclusão.
        if (consulta.PesquisaSatisfacao == null)
        {
            context.NotificacoesSimuladas.AddRange(
                ConvitePesquisa(consulta, CanalNotificacao.WhatsApp,
                    $"Pesquisa de Satisfação - {consulta.Slot.Instituicao.Nome}",
                    $"Olá {consulta.Paciente.Nome}! Sua consulta com o(a) {consulta.Slot.Medico.Usuario.NomeCompleto} foi finalizada. Por favor, avalie sua experiência."),
                ConvitePesquisa(consulta, CanalNotificacao.Email,
                    "Como foi seu atendimento?",
                    $"Prezado(a) {consulta.Paciente.Nome}, sua opinião é muito importante para nós. Acesse a pesquisa de satisfação em nosso portal."));
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return consulta.ToDto();
    }

    public async Task<HistoricoPacienteDto> ObterHistoricoPacienteAsync(Guid pacienteId, UsuarioAtual solicitante)
    {
        if (solicitante.Perfil is PerfilUsuario.Atendente or PerfilUsuario.Admin)
        {
            throw new AcessoNegadoException("Atendentes e Administradores Técnicos não possuem permissão de acesso ao histórico clínico.");
        }

        var paciente = await context.Pacientes
            .Include(p => p.Consultas).ThenInclude(c => c.Evolucao).ThenInclude(e => e!.Medico).ThenInclude(m => m.Usuario)
            .Include(p => p.Consultas).ThenInclude(c => c.Coleta)
            .Include(p => p.Consultas).ThenInclude(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(p => p.Consultas).ThenInclude(c => c.Slot).ThenInclude(s => s.Instituicao)
            .Include(p => p.Documentos).ThenInclude(d => d.InstituicaoOrigem)
            .Include(p => p.Autorizacoes).ThenInclude(a => a.InstituicaoOrigem)
            .Include(p => p.Autorizacoes).ThenInclude(a => a.InstituicaoDestino)
            .FirstOrDefaultAsync(p => p.Id == pacienteId)
            ?? throw new RegraDeNegocioException("Paciente não encontrado.");

        // Consultas e documentos são filtrados pela instituição de origem que o solicitante pode ver.
        Func<Guid, bool> podeVerOrigem;
        switch (solicitante.Perfil)
        {
            case PerfilUsuario.Paciente:
                if (paciente.UsuarioId != solicitante.Id)
                {
                    throw new AcessoNegadoException("Você não tem permissão para acessar o histórico de outro paciente.");
                }
                podeVerOrigem = _ => true;
                break;

            case PerfilUsuario.Medico:
                var medico = await MedicoDeAsync(solicitante);
                var temConsultaVinculada = await context.Consultas
                    .AnyAsync(c => c.PacienteId == pacienteId && c.Slot.MedicoId == medico.Id);
                if (!temConsultaVinculada)
                {
                    throw new AcessoNegadoException("Médico só pode acessar o histórico de pacientes com consulta vinculada sob sua responsabilidade.");
                }
                podeVerOrigem = origem => origem == medico.InstituicaoId || paciente.TemAutorizacaoAtiva(origem, medico.InstituicaoId);
                break;

            default:
                // Enfermeiro passa pelo controle de acesso, mas não vê consultas nem documentos.
                podeVerOrigem = _ => false;
                break;
        }

        var consultas = paciente.Consultas.Where(c => podeVerOrigem(c.Slot.InstituicaoId)).ToList();
        var documentos = paciente.Documentos.Where(d => podeVerOrigem(d.InstituicaoOrigemId)).ToList();

        return new HistoricoPacienteDto(
            paciente.Id,
            paciente.Nome,
            paciente.DataNascimento,
            consultas.Select(c => c.ToDto()).ToList(),
            consultas.Where(c => c.Evolucao != null).Select(c => c.Evolucao!.ToDto()).ToList(),
            documentos.Select(d => d.ToDto()).ToList(),
            paciente.Autorizacoes.Where(a => a.RevogadaEmUtc == null).Select(a => a.ToDto()).ToList()
        );
    }

    public async Task<DocumentoPacienteDto> AnexarDocumentoAsync(Guid pacienteId, Guid instituicaoOrigemId, IFormFile arquivo, UsuarioAtual solicitante)
    {
        if (solicitante.Perfil is not (PerfilUsuario.Paciente or PerfilUsuario.Medico))
        {
            throw new AcessoNegadoException("Apenas o próprio paciente ou seu médico podem anexar documentos.");
        }

        var paciente = await context.Pacientes.FindAsync(pacienteId)
            ?? throw new RegraDeNegocioException("Paciente não encontrado.");

        if (solicitante.Perfil == PerfilUsuario.Paciente && paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Acesso negado.");
        }

        if (arquivo.Length > TamanhoMaximoBytes)
        {
            throw new RegraDeNegocioException("O arquivo excede o limite máximo permitido de 5 MB.");
        }

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao))
        {
            throw new RegraDeNegocioException("Formato de arquivo não suportado. Utilize PDF, JPG ou PNG.");
        }

        var chaveArquivo = await arquivos.SalvarAsync(arquivo, extensao);

        var instituicao = await context.Instituicoes.FindAsync(instituicaoOrigemId);

        var documento = new DocumentoPaciente
        {
            PacienteId = pacienteId,
            Paciente = paciente,
            InstituicaoOrigemId = instituicaoOrigemId,
            InstituicaoOrigem = instituicao!,
            NomeOriginal = Path.GetFileName(arquivo.FileName),
            Tipo = arquivo.ContentType,
            Extensao = extensao,
            TamanhoBytes = arquivo.Length,
            ChaveArquivo = chaveArquivo,
            CriadoEmUtc = DateTime.UtcNow
        };

        context.DocumentosPaciente.Add(documento);
        await context.SaveChangesAsync();

        return documento.ToDto();
    }

    public async Task<List<DocumentoPacienteDto>> ListarDocumentosAsync(Guid pacienteId, UsuarioAtual solicitante)
    {
        var historico = await ObterHistoricoPacienteAsync(pacienteId, solicitante);
        return historico.Documentos;
    }

    public async Task<(byte[] Bytes, string ContentType, string NomeArquivo)?> ObterArquivoParaDownloadAsync(Guid documentoId, UsuarioAtual solicitante)
    {
        if (solicitante.Perfil is PerfilUsuario.Atendente or PerfilUsuario.Admin)
        {
            throw new AcessoNegadoException("Atendentes e Administradores não possuem permissão para download de arquivos clínicos.");
        }

        var documento = await context.DocumentosPaciente
            .Include(d => d.Paciente).ThenInclude(p => p.Autorizacoes)
            .FirstOrDefaultAsync(d => d.Id == documentoId);

        if (documento == null) return null;

        if (solicitante.Perfil == PerfilUsuario.Paciente && documento.Paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Acesso negado.");
        }

        if (solicitante.Perfil == PerfilUsuario.Medico)
        {
            var medico = await MedicoDeAsync(solicitante);

            var temConsulta = await context.Consultas.AnyAsync(c => c.PacienteId == documento.PacienteId && c.Slot.MedicoId == medico.Id);
            if (!temConsulta) throw new AcessoNegadoException("Acesso negado.");

            if (documento.InstituicaoOrigemId != medico.InstituicaoId
                && !documento.Paciente.TemAutorizacaoAtiva(documento.InstituicaoOrigemId, medico.InstituicaoId))
            {
                throw new AcessoNegadoException("Acesso negado: Autorização de compartilhamento ausente ou revogada.");
            }
        }

        // Documentos do seed não existem em disco: entrega um conteúdo fictício de demonstração.
        var bytes = await arquivos.LerAsync(documento.ChaveArquivo)
            ?? Encoding.UTF8.GetBytes($"[Arquivo Fictício de Demonstração]: {documento.NomeOriginal}\nPaciente ID: {documento.PacienteId}\nOrigem ID: {documento.InstituicaoOrigemId}");

        return (bytes, documento.Tipo, documento.NomeOriginal);
    }

    public async Task<AutorizacaoDto> ConcederAutorizacaoAsync(Guid pacienteId, CriarAutorizacaoDto dto, UsuarioAtual solicitante)
    {
        var paciente = await context.Pacientes.FindAsync(pacienteId)
            ?? throw new RegraDeNegocioException("Paciente não encontrado.");

        if (paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Apenas o próprio paciente pode conceder autorizações de compartilhamento.");
        }

        if (dto.InstituicaoOrigemId == dto.InstituicaoDestinoId)
        {
            throw new RegraDeNegocioException("Instituição de origem e destino devem ser diferentes.");
        }

        var origem = await context.Instituicoes.FindAsync(dto.InstituicaoOrigemId);
        var destino = await context.Instituicoes.FindAsync(dto.InstituicaoDestinoId);

        var autorizacao = new AutorizacaoCompartilhamento
        {
            PacienteId = pacienteId,
            Paciente = paciente,
            InstituicaoOrigemId = dto.InstituicaoOrigemId,
            InstituicaoOrigem = origem!,
            InstituicaoDestinoId = dto.InstituicaoDestinoId,
            InstituicaoDestino = destino!,
            CriadaEmUtc = DateTime.UtcNow,
            RevogadaEmUtc = null
        };

        context.AutorizacoesCompartilhamento.Add(autorizacao);
        await context.SaveChangesAsync();

        return autorizacao.ToDto();
    }

    public async Task<bool> RevogarAutorizacaoAsync(Guid autorizacaoId, UsuarioAtual solicitante)
    {
        var autorizacao = await context.AutorizacoesCompartilhamento
            .Include(a => a.Paciente)
            .FirstOrDefaultAsync(a => a.Id == autorizacaoId);

        if (autorizacao == null) return false;

        if (autorizacao.Paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Você só pode revogar autorizações do seu próprio cadastro.");
        }

        if (autorizacao.RevogadaEmUtc == null)
        {
            autorizacao.RevogadaEmUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<List<AutorizacaoDto>> ListarAutorizacoesAsync(Guid pacienteId, UsuarioAtual solicitante)
    {
        var paciente = await context.Pacientes.FindAsync(pacienteId)
            ?? throw new RegraDeNegocioException("Paciente não encontrado.");

        if (paciente.UsuarioId != solicitante.Id)
        {
            throw new AcessoNegadoException("Acesso negado.");
        }

        var autorizacoes = await context.AutorizacoesCompartilhamento
            .Include(a => a.Paciente)
            .Include(a => a.InstituicaoOrigem)
            .Include(a => a.InstituicaoDestino)
            .Where(a => a.PacienteId == pacienteId)
            .OrderByDescending(a => a.CriadaEmUtc)
            .ToListAsync();

        return autorizacoes.Select(a => a.ToDto()).ToList();
    }

    private static NotificacaoSimulada ConvitePesquisa(Consulta consulta, CanalNotificacao canal, string titulo, string texto) => new()
    {
        PacienteId = consulta.PacienteId,
        ConsultaId = consulta.Id,
        Canal = canal,
        Finalidade = FinalidadeNotificacao.ConvitePesquisa,
        Titulo = titulo,
        Texto = texto,
        CriadaEmUtc = DateTime.UtcNow
    };

    /// <summary>Registro de Medico do solicitante, para regras que dependem da instituição dele.</summary>
    private async Task<Medico> MedicoDeAsync(UsuarioAtual solicitante)
        => await context.Medicos.FindAsync(solicitante.MedicoId)
           ?? throw new AcessoNegadoException("Médico não encontrado.");
}
