using System.Text;
using Consultorio.Api.Data;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Services;

public interface IProntuarioService
{
    Task<EvolucaoDto> RegistrarEvolucaoAsync(Guid consultaId, Guid medicoUsuarioId, RegistrarEvolucaoDto dto);
    Task<ConsultaDto> ConcluirConsultaAsync(Guid consultaId, Guid medicoUsuarioId);
    Task<HistoricoPacienteDto> ObterHistoricoPacienteAsync(Guid pacienteId, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<DocumentoPacienteDto> AnexarDocumentoAsync(Guid pacienteId, Guid instituicaoOrigemId, IFormFile arquivo, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<List<DocumentoPacienteDto>> ListarDocumentosAsync(Guid pacienteId, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<(byte[] Bytes, string ContentType, string NomeArquivo)?> ObterArquivoParaDownloadAsync(Guid documentoId, Guid solicitanteUsuarioId, PerfilUsuario perfil);
    Task<AutorizacaoDto> ConcederAutorizacaoAsync(Guid pacienteId, CriarAutorizacaoDto dto, Guid pacienteUsuarioId);
    Task<bool> RevogarAutorizacaoAsync(Guid autorizacaoId, Guid pacienteUsuarioId);
    Task<List<AutorizacaoDto>> ListarAutorizacoesAsync(Guid pacienteId, Guid pacienteUsuarioId);
}

public class ProntuarioService : IProntuarioService
{
    private const long TamanhoMaximoBytes = 5 * 1024 * 1024;
    private static readonly string[] ExtensoesPermitidas = [".pdf", ".jpg", ".jpeg", ".png"];

    private readonly AppDbContext _context;
    private readonly string _uploadDir;

    public ProntuarioService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _uploadDir = configuration["Storage:UploadDirectory"] ?? "./uploads";
        Directory.CreateDirectory(_uploadDir);
    }

    public async Task<EvolucaoDto> RegistrarEvolucaoAsync(Guid consultaId, Guid medicoUsuarioId, RegistrarEvolucaoDto dto)
    {
        var medico = await MedicoDoUsuarioAsync(medicoUsuarioId, "Usuário não é um médico cadastrado.");

        var consulta = await _context.Consultas
            .Include(c => c.Slot)
            .Include(c => c.Evolucao).ThenInclude(e => e!.Medico).ThenInclude(m => m.Usuario)
            .FirstOrDefaultAsync(c => c.Id == consultaId)
            ?? throw new InvalidOperationException("Consulta não encontrada.");

        if (consulta.Slot.MedicoId != medico.Id)
        {
            throw new UnauthorizedAccessException("Apenas o médico responsável por esta consulta pode registrar a evolução.");
        }

        if (string.IsNullOrWhiteSpace(dto.Texto))
        {
            throw new InvalidOperationException("O texto da evolução clínica é obrigatório.");
        }

        if (consulta.Evolucao == null)
        {
            consulta.Evolucao = new Evolucao { ConsultaId = consulta.Id, MedicoId = medico.Id, Medico = medico };
            _context.Evolucoes.Add(consulta.Evolucao);
        }

        consulta.Evolucao.Texto = dto.Texto;
        consulta.Evolucao.CriadaEmUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return consulta.Evolucao.ToDto();
    }

    public async Task<ConsultaDto> ConcluirConsultaAsync(Guid consultaId, Guid medicoUsuarioId)
    {
        var medico = await MedicoDoUsuarioAsync(medicoUsuarioId, "Usuário não é um médico.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        var consulta = await _context.Consultas.ComDetalhes().FirstOrDefaultAsync(c => c.Id == consultaId)
            ?? throw new InvalidOperationException("Consulta não encontrada.");

        if (consulta.Slot.MedicoId != medico.Id)
        {
            throw new UnauthorizedAccessException("Apenas o médico responsável pode concluir o atendimento.");
        }

        if (consulta.Evolucao == null || string.IsNullOrWhiteSpace(consulta.Evolucao.Texto))
        {
            throw new InvalidOperationException("É obrigatório registrar a evolução médica antes de concluir o atendimento.");
        }

        if (consulta.Status == StatusConsulta.Concluida)
        {
            return consulta.ToDto();
        }

        consulta.Status = StatusConsulta.Concluida;

        // Convite de pesquisa gerado uma única vez, na mesma transação da conclusão.
        if (consulta.PesquisaSatisfacao == null)
        {
            _context.NotificacoesSimuladas.AddRange(
                ConvitePesquisa(consulta, CanalNotificacao.WhatsApp,
                    $"Pesquisa de Satisfação - {consulta.Slot.Instituicao.Nome}",
                    $"Olá {consulta.Paciente.Nome}! Sua consulta com o(a) {consulta.Slot.Medico.Usuario.NomeCompleto} foi finalizada. Por favor, avalie sua experiência."),
                ConvitePesquisa(consulta, CanalNotificacao.Email,
                    "Como foi seu atendimento?",
                    $"Prezado(a) {consulta.Paciente.Nome}, sua opinião é muito importante para nós. Acesse a pesquisa de satisfação em nosso portal."));
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return consulta.ToDto();
    }

    public async Task<HistoricoPacienteDto> ObterHistoricoPacienteAsync(Guid pacienteId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        if (perfil is PerfilUsuario.Atendente or PerfilUsuario.Admin)
        {
            throw new UnauthorizedAccessException("Atendentes e Administradores Técnicos não possuem permissão de acesso ao histórico clínico.");
        }

        var paciente = await _context.Pacientes
            .Include(p => p.Consultas).ThenInclude(c => c.Evolucao).ThenInclude(e => e!.Medico).ThenInclude(m => m.Usuario)
            .Include(p => p.Consultas).ThenInclude(c => c.Coleta)
            .Include(p => p.Consultas).ThenInclude(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(p => p.Consultas).ThenInclude(c => c.Slot).ThenInclude(s => s.Instituicao)
            .Include(p => p.Documentos).ThenInclude(d => d.InstituicaoOrigem)
            .Include(p => p.Autorizacoes).ThenInclude(a => a.InstituicaoOrigem)
            .Include(p => p.Autorizacoes).ThenInclude(a => a.InstituicaoDestino)
            .FirstOrDefaultAsync(p => p.Id == pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        // Consultas e documentos são filtrados pela instituição de origem que o solicitante pode ver.
        Func<Guid, bool> podeVerOrigem;
        switch (perfil)
        {
            case PerfilUsuario.Paciente:
                if (paciente.UsuarioId != solicitanteUsuarioId)
                {
                    throw new UnauthorizedAccessException("Você não tem permissão para acessar o histórico de outro paciente.");
                }
                podeVerOrigem = _ => true;
                break;

            case PerfilUsuario.Medico:
                var medico = await MedicoDoUsuarioAsync(solicitanteUsuarioId, "Médico não encontrado.");
                var temConsultaVinculada = await _context.Consultas
                    .AnyAsync(c => c.PacienteId == pacienteId && c.Slot.MedicoId == medico.Id);
                if (!temConsultaVinculada)
                {
                    throw new UnauthorizedAccessException("Médico só pode acessar o histórico de pacientes com consulta vinculada sob sua responsabilidade.");
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

    public async Task<DocumentoPacienteDto> AnexarDocumentoAsync(Guid pacienteId, Guid instituicaoOrigemId, IFormFile arquivo, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        if (perfil is not (PerfilUsuario.Paciente or PerfilUsuario.Medico))
        {
            throw new UnauthorizedAccessException("Apenas o próprio paciente ou seu médico podem anexar documentos.");
        }

        var paciente = await _context.Pacientes.FindAsync(pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (perfil == PerfilUsuario.Paciente && paciente.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        if (arquivo.Length > TamanhoMaximoBytes)
        {
            throw new InvalidOperationException("O arquivo excede o limite máximo permitido de 5 MB.");
        }

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (!ExtensoesPermitidas.Contains(extensao))
        {
            throw new InvalidOperationException("Formato de arquivo não suportado. Utilize PDF, JPG ou PNG.");
        }

        var chaveArquivo = $"{Guid.NewGuid()}{extensao}";
        await using (var stream = File.Create(Path.Combine(_uploadDir, chaveArquivo)))
        {
            await arquivo.CopyToAsync(stream);
        }

        var instituicao = await _context.Instituicoes.FindAsync(instituicaoOrigemId);

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

        _context.DocumentosPaciente.Add(documento);
        await _context.SaveChangesAsync();

        return documento.ToDto();
    }

    public async Task<List<DocumentoPacienteDto>> ListarDocumentosAsync(Guid pacienteId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        var historico = await ObterHistoricoPacienteAsync(pacienteId, solicitanteUsuarioId, perfil);
        return historico.Documentos;
    }

    public async Task<(byte[] Bytes, string ContentType, string NomeArquivo)?> ObterArquivoParaDownloadAsync(Guid documentoId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        if (perfil is PerfilUsuario.Atendente or PerfilUsuario.Admin)
        {
            throw new UnauthorizedAccessException("Atendentes e Administradores não possuem permissão para download de arquivos clínicos.");
        }

        var documento = await _context.DocumentosPaciente
            .Include(d => d.Paciente).ThenInclude(p => p.Autorizacoes)
            .FirstOrDefaultAsync(d => d.Id == documentoId);

        if (documento == null) return null;

        if (perfil == PerfilUsuario.Paciente && documento.Paciente.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        if (perfil == PerfilUsuario.Medico)
        {
            var medico = await MedicoDoUsuarioAsync(solicitanteUsuarioId, "Médico não encontrado.");

            var temConsulta = await _context.Consultas.AnyAsync(c => c.PacienteId == documento.PacienteId && c.Slot.MedicoId == medico.Id);
            if (!temConsulta) throw new UnauthorizedAccessException("Acesso negado.");

            if (documento.InstituicaoOrigemId != medico.InstituicaoId
                && !documento.Paciente.TemAutorizacaoAtiva(documento.InstituicaoOrigemId, medico.InstituicaoId))
            {
                throw new UnauthorizedAccessException("Acesso negado: Autorização de compartilhamento ausente ou revogada.");
            }
        }

        var caminho = Path.Combine(_uploadDir, documento.ChaveArquivo);
        var bytes = File.Exists(caminho)
            ? await File.ReadAllBytesAsync(caminho)
            // Documentos do seed não existem em disco: entrega um conteúdo fictício de demonstração.
            : Encoding.UTF8.GetBytes($"[Arquivo Fictício de Demonstração]: {documento.NomeOriginal}\nPaciente ID: {documento.PacienteId}\nOrigem ID: {documento.InstituicaoOrigemId}");

        return (bytes, documento.Tipo, documento.NomeOriginal);
    }

    public async Task<AutorizacaoDto> ConcederAutorizacaoAsync(Guid pacienteId, CriarAutorizacaoDto dto, Guid pacienteUsuarioId)
    {
        var paciente = await _context.Pacientes.FindAsync(pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new UnauthorizedAccessException("Apenas o próprio paciente pode conceder autorizações de compartilhamento.");
        }

        if (dto.InstituicaoOrigemId == dto.InstituicaoDestinoId)
        {
            throw new InvalidOperationException("Instituição de origem e destino devem ser diferentes.");
        }

        var origem = await _context.Instituicoes.FindAsync(dto.InstituicaoOrigemId);
        var destino = await _context.Instituicoes.FindAsync(dto.InstituicaoDestinoId);

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

        _context.AutorizacoesCompartilhamento.Add(autorizacao);
        await _context.SaveChangesAsync();

        return autorizacao.ToDto();
    }

    public async Task<bool> RevogarAutorizacaoAsync(Guid autorizacaoId, Guid pacienteUsuarioId)
    {
        var autorizacao = await _context.AutorizacoesCompartilhamento
            .Include(a => a.Paciente)
            .FirstOrDefaultAsync(a => a.Id == autorizacaoId);

        if (autorizacao == null) return false;

        if (autorizacao.Paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new UnauthorizedAccessException("Você só pode revogar autorizações do seu próprio cadastro.");
        }

        if (autorizacao.RevogadaEmUtc == null)
        {
            autorizacao.RevogadaEmUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<List<AutorizacaoDto>> ListarAutorizacoesAsync(Guid pacienteId, Guid pacienteUsuarioId)
    {
        var paciente = await _context.Pacientes.FindAsync(pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        var autorizacoes = await _context.AutorizacoesCompartilhamento
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

    private async Task<Medico> MedicoDoUsuarioAsync(Guid usuarioId, string mensagemSeAusente)
        => await _context.Medicos.Include(m => m.Usuario).FirstOrDefaultAsync(m => m.UsuarioId == usuarioId)
           ?? throw new UnauthorizedAccessException(mensagemSeAusente);
}
