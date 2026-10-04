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
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly string _uploadDir;

    public ProntuarioService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _uploadDir = _configuration["Storage:UploadDirectory"] ?? "./uploads";

        if (!Directory.Exists(_uploadDir))
        {
            Directory.CreateDirectory(_uploadDir);
        }
    }

    public async Task<EvolucaoDto> RegistrarEvolucaoAsync(Guid consultaId, Guid medicoUsuarioId, RegistrarEvolucaoDto dto)
    {
        var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == medicoUsuarioId)
            ?? throw new UnauthorizedAccessException("Usuário não é um médico cadastrado.");

        var consulta = await _context.Consultas
            .Include(c => c.Slot)
            .Include(c => c.Evolucao)
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

        if (consulta.Evolucao != null)
        {
            consulta.Evolucao.Texto = dto.Texto;
            consulta.Evolucao.CriadaEmUtc = DateTime.UtcNow;
        }
        else
        {
            consulta.Evolucao = new Evolucao
            {
                Id = Guid.NewGuid(),
                ConsultaId = consulta.Id,
                MedicoId = medico.Id,
                Texto = dto.Texto,
                CriadaEmUtc = DateTime.UtcNow
            };
            await _context.Evolucoes.AddAsync(consulta.Evolucao);
        }

        await _context.SaveChangesAsync();

        var medUser = await _context.Users.FindAsync(medicoUsuarioId);

        return new EvolucaoDto(
            consulta.Evolucao.Id,
            consulta.Id,
            medico.Id,
            medUser?.NomeCompleto ?? "Médico",
            consulta.Evolucao.Texto,
            consulta.Evolucao.CriadaEmUtc
        );
    }

    public async Task<ConsultaDto> ConcluirConsultaAsync(Guid consultaId, Guid medicoUsuarioId)
    {
        var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == medicoUsuarioId)
            ?? throw new UnauthorizedAccessException("Usuário não é um médico.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Coleta)
            .Include(c => c.Evolucao)
            .Include(c => c.PesquisaSatisfacao)
            .Include(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(c => c.Slot).ThenInclude(s => s.Instituicao)
            .FirstOrDefaultAsync(c => c.Id == consultaId)
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
            return MapearConsultaDto(consulta);
        }

        consulta.Status = StatusConsulta.Concluida;

        // Gerar Convite de Pesquisa de Satisfação na mesma transação (uma única vez)
        if (consulta.PesquisaSatisfacao == null)
        {
            var notifWhatsapp = new NotificacaoSimulada
            {
                Id = Guid.NewGuid(),
                PacienteId = consulta.PacienteId,
                ConsultaId = consulta.Id,
                Canal = CanalNotificacao.WhatsApp,
                Finalidade = FinalidadeNotificacao.ConvitePesquisa,
                Titulo = $"Pesquisa de Satisfação - {consulta.Slot.Instituicao.Nome}",
                Texto = $"Olá {consulta.Paciente.Nome}! Sua consulta com o(a) {consulta.Slot.Medico.Usuario.NomeCompleto} foi finalizada. Por favor, avalie sua experiência.",
                CriadaEmUtc = DateTime.UtcNow
            };

            var notifEmail = new NotificacaoSimulada
            {
                Id = Guid.NewGuid(),
                PacienteId = consulta.PacienteId,
                ConsultaId = consulta.Id,
                Canal = CanalNotificacao.Email,
                Finalidade = FinalidadeNotificacao.ConvitePesquisa,
                Titulo = "Como foi seu atendimento?",
                Texto = $"Prezado(a) {consulta.Paciente.Nome}, sua opinião é muito importante para nós. Acesse a pesquisa de satisfação em nosso portal.",
                CriadaEmUtc = DateTime.UtcNow
            };

            await _context.NotificacoesSimuladas.AddRangeAsync(notifWhatsapp, notifEmail);
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return MapearConsultaDto(consulta);
    }

    public async Task<HistoricoPacienteDto> ObterHistoricoPacienteAsync(Guid pacienteId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        // 1. Atendente e Admin Técnico NUNCA acessam histórico clínico
        if (perfil == PerfilUsuario.Atendente || perfil == PerfilUsuario.Admin)
        {
            throw new UnauthorizedAccessException("Atendentes e Administradores Técnicos não possuem permissão de acesso ao histórico clínico.");
        }

        var paciente = await _context.Pacientes
            .Include(p => p.Consultas).ThenInclude(c => c.Evolucao)
            .Include(p => p.Consultas).ThenInclude(c => c.Coleta)
            .Include(p => p.Consultas).ThenInclude(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)
            .Include(p => p.Consultas).ThenInclude(c => c.Slot).ThenInclude(s => s.Instituicao)
            .Include(p => p.Documentos).ThenInclude(d => d.InstituicaoOrigem)
            .Include(p => p.Autorizacoes).ThenInclude(a => a.InstituicaoOrigem)
            .Include(p => p.Autorizacoes).ThenInclude(a => a.InstituicaoDestino)
            .FirstOrDefaultAsync(p => p.Id == pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        // 2. Se for Paciente, só pode ver seu próprio histórico
        if (perfil == PerfilUsuario.Paciente)
        {
            if (paciente.UsuarioId != solicitanteUsuarioId)
            {
                throw new UnauthorizedAccessException("Você não tem permissão para acessar o histórico de outro paciente.");
            }
        }
        else if (perfil == PerfilUsuario.Medico)
        {
            // 3. Se for Médico: precisa ter consulta com o paciente
            var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == solicitanteUsuarioId)
                ?? throw new UnauthorizedAccessException("Médico não encontrado.");

            var temConsultaVinculada = await _context.Consultas
                .AnyAsync(c => c.PacienteId == pacienteId && c.Slot.MedicoId == medico.Id);

            if (!temConsultaVinculada)
            {
                throw new UnauthorizedAccessException("Médico só pode acessar o histórico de pacientes com consulta vinculada sob sua responsabilidade.");
            }
        }

        // Filtragem de dados com base no consentimento de compartilhamento entre instituições
        var consultasAutorizadas = new List<Consulta>();
        var documentosAutorizados = new List<DocumentoPaciente>();

        if (perfil == PerfilUsuario.Paciente)
        {
            consultasAutorizadas = paciente.Consultas.ToList();
            documentosAutorizados = paciente.Documentos.ToList();
        }
        else if (perfil == PerfilUsuario.Medico)
        {
            var medico = await _context.Medicos.FirstAsync(m => m.UsuarioId == solicitanteUsuarioId);
            var instMedico = medico.InstituicaoId;

            // Consultas da própria instituição do médico OU de outra instituição se houver autorização ativa
            foreach (var cons in paciente.Consultas)
            {
                if (cons.Slot.InstituicaoId == instMedico)
                {
                    consultasAutorizadas.Add(cons);
                }
                else
                {
                    var temAutorizacao = paciente.Autorizacoes.Any(a => 
                        a.InstituicaoOrigemId == cons.Slot.InstituicaoId && 
                        a.InstituicaoDestinoId == instMedico && 
                        a.RevogadaEmUtc == null);

                    if (temAutorizacao)
                    {
                        consultasAutorizadas.Add(cons);
                    }
                }
            }

            foreach (var doc in paciente.Documentos)
            {
                if (doc.InstituicaoOrigemId == instMedico)
                {
                    documentosAutorizados.Add(doc);
                }
                else
                {
                    var temAutorizacao = paciente.Autorizacoes.Any(a => 
                        a.InstituicaoOrigemId == doc.InstituicaoOrigemId && 
                        a.InstituicaoDestinoId == instMedico && 
                        a.RevogadaEmUtc == null);

                    if (temAutorizacao)
                    {
                        documentosAutorizados.Add(doc);
                    }
                }
            }
        }

        var consultasDto = consultasAutorizadas.Select(MapearConsultaDto).ToList();

        var evolucoesDto = consultasAutorizadas
            .Where(c => c.Evolucao != null)
            .Select(c => new EvolucaoDto(
                c.Evolucao!.Id,
                c.Id,
                c.Evolucao.MedicoId,
                c.Slot.Medico.Usuario.NomeCompleto,
                c.Evolucao.Texto,
                c.Evolucao.CriadaEmUtc
            )).ToList();

        var docsDto = documentosAutorizados.Select(d => new DocumentoPacienteDto(
            d.Id,
            d.PacienteId,
            d.InstituicaoOrigemId,
            d.InstituicaoOrigem.Nome,
            d.NomeOriginal,
            d.Tipo,
            d.Extensao,
            d.TamanhoBytes,
            d.CriadoEmUtc
        )).ToList();

        var autsDto = paciente.Autorizacoes
            .Where(a => a.RevogadaEmUtc == null)
            .Select(a => new AutorizacaoDto(
                a.Id,
                a.PacienteId,
                paciente.Nome,
                a.InstituicaoOrigemId,
                a.InstituicaoOrigem.Nome,
                a.InstituicaoDestinoId,
                a.InstituicaoDestino.Nome,
                a.CriadaEmUtc,
                a.RevogadaEmUtc,
                a.RevogadaEmUtc == null
            )).ToList();

        return new HistoricoPacienteDto(
            paciente.Id,
            paciente.Nome,
            paciente.DataNascimento,
            consultasDto,
            evolucoesDto,
            docsDto,
            autsDto
        );
    }

    public async Task<DocumentoPacienteDto> AnexarDocumentoAsync(Guid pacienteId, Guid instituicaoOrigemId, IFormFile arquivo, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        if (perfil != PerfilUsuario.Paciente && perfil != PerfilUsuario.Medico)
        {
            throw new UnauthorizedAccessException("Apenas o próprio paciente ou seu médico podem anexar documentos.");
        }

        var paciente = await _context.Pacientes.FindAsync(pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (perfil == PerfilUsuario.Paciente && paciente.UsuarioId != solicitanteUsuarioId)
        {
            throw new UnauthorizedAccessException("Acesso negado.");
        }

        // Validação de tamanho (máximo 5MB)
        if (arquivo.Length > 5 * 1024 * 1024)
        {
            throw new InvalidOperationException("O arquivo excede o limite máximo permitido de 5 MB.");
        }

        var extensoesValidas = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

        if (!extensoesValidas.Contains(ext))
        {
            throw new InvalidOperationException("Formato de arquivo não suportado. Utilize PDF, JPG ou PNG.");
        }

        var chaveUnica = $"{Guid.NewGuid()}{ext}";
        var caminhoCompleto = Path.Combine(_uploadDir, chaveUnica);

        using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
        {
            await arquivo.CopyToAsync(stream);
        }

        var doc = new DocumentoPaciente
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId,
            InstituicaoOrigemId = instituicaoOrigemId,
            NomeOriginal = Path.GetFileName(arquivo.FileName),
            Tipo = arquivo.ContentType,
            Extensao = ext,
            TamanhoBytes = arquivo.Length,
            ChaveArquivo = chaveUnica,
            CriadoEmUtc = DateTime.UtcNow
        };

        await _context.DocumentosPaciente.AddAsync(doc);
        await _context.SaveChangesAsync();

        var inst = await _context.Instituicoes.FindAsync(instituicaoOrigemId);

        return new DocumentoPacienteDto(
            doc.Id,
            doc.PacienteId,
            doc.InstituicaoOrigemId,
            inst?.Nome ?? "Instituição",
            doc.NomeOriginal,
            doc.Tipo,
            doc.Extensao,
            doc.TamanhoBytes,
            doc.CriadoEmUtc
        );
    }

    public async Task<List<DocumentoPacienteDto>> ListarDocumentosAsync(Guid pacienteId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        var hist = await ObterHistoricoPacienteAsync(pacienteId, solicitanteUsuarioId, perfil);
        return hist.Documentos;
    }

    public async Task<(byte[] Bytes, string ContentType, string NomeArquivo)?> ObterArquivoParaDownloadAsync(Guid documentoId, Guid solicitanteUsuarioId, PerfilUsuario perfil)
    {
        if (perfil == PerfilUsuario.Atendente || perfil == PerfilUsuario.Admin)
        {
            throw new UnauthorizedAccessException("Atendentes e Administradores não possuem permissão para download de arquivos clínicos.");
        }

        var doc = await _context.DocumentosPaciente
            .Include(d => d.Paciente)
            .FirstOrDefaultAsync(d => d.Id == documentoId);

        if (doc == null) return null;

        // Validar autorização
        if (perfil == PerfilUsuario.Paciente)
        {
            if (doc.Paciente.UsuarioId != solicitanteUsuarioId)
                throw new UnauthorizedAccessException("Acesso negado.");
        }
        else if (perfil == PerfilUsuario.Medico)
        {
            var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.UsuarioId == solicitanteUsuarioId)
                ?? throw new UnauthorizedAccessException("Médico não encontrado.");

            var temConsulta = await _context.Consultas.AnyAsync(c => c.PacienteId == doc.PacienteId && c.Slot.MedicoId == medico.Id);
            if (!temConsulta) throw new UnauthorizedAccessException("Acesso negado.");

            if (doc.InstituicaoOrigemId != medico.InstituicaoId)
            {
                var temAut = await _context.AutorizacoesCompartilhamento.AnyAsync(a => 
                    a.PacienteId == doc.PacienteId && 
                    a.InstituicaoOrigemId == doc.InstituicaoOrigemId && 
                    a.InstituicaoDestinoId == medico.InstituicaoId && 
                    a.RevogadaEmUtc == null);

                if (!temAut) throw new UnauthorizedAccessException("Acesso negado: Autorização de compartilhamento ausente ou revogada.");
            }
        }

        var caminho = Path.Combine(_uploadDir, doc.ChaveArquivo);
        byte[] bytes;

        if (File.Exists(caminho))
        {
            bytes = await File.ReadAllBytesAsync(caminho);
        }
        else
        {
            // Arquivo fictício gerado em memória para demonstração caso não esteja no disco
            bytes = System.Text.Encoding.UTF8.GetBytes($"[Arquivo Fictício de Demonstração]: {doc.NomeOriginal}\nPaciente ID: {doc.PacienteId}\nOrigem ID: {doc.InstituicaoOrigemId}");
        }

        return (bytes, doc.Tipo, doc.NomeOriginal);
    }

    public async Task<AutorizacaoDto> ConcederAutorizacaoAsync(Guid pacienteId, CriarAutorizacaoDto dto, Guid pacienteUsuarioId)
    {
        var paciente = await _context.Pacientes
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == pacienteId)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new UnauthorizedAccessException("Apenas o próprio paciente pode conceder autorizações de compartilhamento.");
        }

        if (dto.InstituicaoOrigemId == dto.InstituicaoDestinoId)
        {
            throw new InvalidOperationException("Instituição de origem e destino devem ser diferentes.");
        }

        var aut = new AutorizacaoCompartilhamento
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId,
            InstituicaoOrigemId = dto.InstituicaoOrigemId,
            InstituicaoDestinoId = dto.InstituicaoDestinoId,
            CriadaEmUtc = DateTime.UtcNow,
            RevogadaEmUtc = null
        };

        await _context.AutorizacoesCompartilhamento.AddAsync(aut);
        await _context.SaveChangesAsync();

        var instOrigem = await _context.Instituicoes.FindAsync(dto.InstituicaoOrigemId);
        var instDestino = await _context.Instituicoes.FindAsync(dto.InstituicaoDestinoId);

        return new AutorizacaoDto(
            aut.Id,
            pacienteId,
            paciente.Nome,
            dto.InstituicaoOrigemId,
            instOrigem?.Nome ?? "Origem",
            dto.InstituicaoDestinoId,
            instDestino?.Nome ?? "Destino",
            aut.CriadaEmUtc,
            null,
            true
        );
    }

    public async Task<bool> RevogarAutorizacaoAsync(Guid autorizacaoId, Guid pacienteUsuarioId)
    {
        var aut = await _context.AutorizacoesCompartilhamento
            .Include(a => a.Paciente)
            .FirstOrDefaultAsync(a => a.Id == autorizacaoId);

        if (aut == null) return false;

        if (aut.Paciente.UsuarioId != pacienteUsuarioId)
        {
            throw new UnauthorizedAccessException("Você só pode revogar autorizações do seu próprio cadastro.");
        }

        if (aut.RevogadaEmUtc == null)
        {
            aut.RevogadaEmUtc = DateTime.UtcNow;
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

        var auts = await _context.AutorizacoesCompartilhamento
            .Include(a => a.InstituicaoOrigem)
            .Include(a => a.InstituicaoDestino)
            .Where(a => a.PacienteId == pacienteId)
            .OrderByDescending(a => a.CriadaEmUtc)
            .ToListAsync();

        return auts.Select(a => new AutorizacaoDto(
            a.Id,
            a.PacienteId,
            paciente.Nome,
            a.InstituicaoOrigemId,
            a.InstituicaoOrigem.Nome,
            a.InstituicaoDestinoId,
            a.InstituicaoDestino.Nome,
            a.CriadaEmUtc,
            a.RevogadaEmUtc,
            a.RevogadaEmUtc == null
        )).ToList();
    }

    private static ConsultaDto MapearConsultaDto(Consulta c)
    {
        return new ConsultaDto(
            c.Id,
            c.PacienteId,
            c.Paciente?.Nome ?? "Paciente",
            c.Slot?.MedicoId ?? Guid.Empty,
            c.Slot?.Medico?.Usuario?.NomeCompleto ?? "Médico",
            c.Slot?.Medico?.Especialidade ?? "Especialidade",
            c.Slot?.InstituicaoId ?? Guid.Empty,
            c.Slot?.Instituicao?.Nome ?? "Instituição",
            c.SlotId,
            c.Slot?.InicioUtc ?? DateTime.MinValue,
            c.Slot?.FimUtc ?? DateTime.MinValue,
            c.ColetaId,
            c.Coleta?.ResumoValidado,
            c.Status,
            c.CriadaEmUtc,
            c.Evolucao != null,
            c.PesquisaSatisfacao != null
        );
    }
}
