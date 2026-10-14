using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

// RegraDeNegocioException, ConflitoException e AcessoNegadoException viram { mensagem } no TratadorGlobalDeExcecoes.
public abstract class ConsultorioControllerBase : ControllerBase
{
    private UsuarioAtual? _solicitante;

    protected UsuarioAtual Solicitante => _solicitante ??= UsuarioAtual.De(User);
}

// 1. Instituições & Médicos
[ApiController]
[Route("api/[controller]")]
public class InstituicoesController(IInstituicaoService service) : ConsultorioControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar() => Ok(await service.ListarInstituicoesAsync());
}

[ApiController]
[Route("api/[controller]")]
public class MedicosController(IInstituicaoService service) : ConsultorioControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromQuery] Guid? instituicaoId) 
        => Ok(await service.ListarMedicosAsync(instituicaoId));
}

// 2. Pacientes
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PacientesController(IPacienteService pacienteService, IProntuarioService prontuarioService) : ConsultorioControllerBase
{
    [HttpGet]
    [Authorize(Roles = Papeis.Equipe)]
    public async Task<IActionResult> Listar() => Ok(await pacienteService.ListarPacientesAsync());

    [HttpPost]
    [Authorize(Roles = Papeis.Recepcao)]
    public async Task<IActionResult> Criar([FromBody] CriarPacienteDto dto) 
        => Ok(await pacienteService.CriarPacienteAsync(dto));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await pacienteService.ObterPacientePorIdAsync(id, Solicitante);
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarPacienteDto dto)
        => Ok(await pacienteService.AtualizarPacienteAsync(id, dto, Solicitante));

    [HttpGet("{id}/historico")]
    public async Task<IActionResult> ObterHistorico(Guid id)
        => Ok(await prontuarioService.ObterHistoricoPacienteAsync(id, Solicitante));

    [HttpPost("{id}/documentos")]
    public async Task<IActionResult> AnexarDocumento(Guid id, [FromQuery] Guid instituicaoOrigemId, IFormFile arquivo)
        => Ok(await prontuarioService.AnexarDocumentoAsync(id, instituicaoOrigemId, arquivo, Solicitante));

    [HttpGet("{id}/documentos")]
    public async Task<IActionResult> ListarDocumentos(Guid id)
        => Ok(await prontuarioService.ListarDocumentosAsync(id, Solicitante));

    [HttpGet("{id}/autorizacoes")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ListarAutorizacoes(Guid id)
        => Ok(await prontuarioService.ListarAutorizacoesAsync(id, Solicitante));

    [HttpPost("{id}/autorizacoes")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ConcederAutorizacao(Guid id, [FromBody] CriarAutorizacaoDto dto)
        => Ok(await prontuarioService.ConcederAutorizacaoAsync(id, dto, Solicitante));
}

// 3. Coletas & Chatbot Guiado
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ColetasController(ITriagemChatService chatService) : ConsultorioControllerBase
{
    [HttpPost]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> Iniciar([FromBody] IniciarColetaDto dto)
    {
        if (Solicitante.PacienteId is not { } pacienteId)
        {
            return Unauthorized();
        }

        return Ok(await chatService.IniciarColetaAsync(pacienteId, dto.InstituicaoId));
    }

    [HttpGet]
    [Authorize(Roles = Papeis.Triagem)]
    public async Task<IActionResult> ListarFila([FromQuery] string? status)
    {
        if (Solicitante.InstituicaoId is null && Solicitante.Perfil != PerfilUsuario.Admin)
        {
            return Forbid();
        }

        return Ok(await chatService.ListarFilaEnfermeiroAsync(Solicitante.InstituicaoId ?? Guid.Empty));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await chatService.ObterConversaPorColetaIdAsync(id, Solicitante);
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPost("{id}/mensagens")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> EnviarMensagem(Guid id, [FromBody] EnviarMensagemChatDto dto)
        => Ok(await chatService.ProcessarMensagemAsync(id, Solicitante, dto));

    [HttpPost("{id}/finalizacao")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> Finalizar(Guid id)
        => Ok(await chatService.FinalizarEnvioEnfermeiroAsync(id, Solicitante));

    [HttpPut("{id}/validacao")]
    [Authorize(Roles = Papeis.Enfermeiro)]
    public async Task<IActionResult> Validar(Guid id, [FromBody] ValidarColetaDto dto)
    {
        var sucesso = await chatService.ValidarColetaAsync(id, Solicitante, dto);
        if (!sucesso)
        {
            return BadRequest(new { mensagem = "Coleta não encontrada ou já validada." });
        }
        return Ok(new { mensagem = "Triagem validada com sucesso!" });
    }
}

// 4. Slots & Agenda
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SlotsController(IAgendaService agendaService) : ConsultorioControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromQuery] Guid? medicoId, [FromQuery] Guid? instituicaoId, [FromQuery] DateTime? data)
        => Ok(await agendaService.ListarSlotsAsync(medicoId, instituicaoId, data));

    [HttpPost]
    [Authorize(Roles = Papeis.Recepcao)]
    public async Task<IActionResult> Criar([FromBody] CriarSlotDto dto)
        => Ok(await agendaService.CriarSlotAsync(dto, Solicitante.InstituicaoId ?? dto.InstituicaoId));
}

// 5. Consultas
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConsultasController(
    IAgendaService agendaService,
    IProntuarioService prontuarioService,
    INotificacaoPesquisaService pesquisaService) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? instituicaoId, [FromQuery] Guid? pacienteId)
        => Ok(await agendaService.ListarConsultasAsync(Solicitante, instituicaoId, pacienteId));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await agendaService.ObterConsultaPorIdAsync(id, Solicitante);
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Agendar([FromBody] AgendarConsultaDto dto)
        => Ok(await agendaService.AgendarConsultaAsync(dto, Solicitante));

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusConsultaDto dto)
    {
        var ok = await agendaService.AtualizarStatusConsultaAsync(id, dto.NovoStatus, Solicitante);
        return ok ? Ok(new { mensagem = "Status atualizado com sucesso." }) : BadRequest();
    }

    [HttpPut("{id}/evolucao")]
    [Authorize(Roles = Papeis.Medico)]
    public async Task<IActionResult> RegistrarEvolucao(Guid id, [FromBody] RegistrarEvolucaoDto dto)
        => Ok(await prontuarioService.RegistrarEvolucaoAsync(id, Solicitante, dto));

    [HttpPost("{id}/conclusao")]
    [Authorize(Roles = Papeis.Medico)]
    public async Task<IActionResult> Concluir(Guid id)
        => Ok(await prontuarioService.ConcluirConsultaAsync(id, Solicitante));

    [HttpGet("{id}/pesquisa")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ObterPesquisa(Guid id)
    {
        var res = await pesquisaService.ObterPesquisaConsultaAsync(id, Solicitante);
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPut("{id}/pesquisa")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ResponderPesquisa(Guid id, [FromBody] ResponderPesquisaDto dto)
        => Ok(await pesquisaService.ResponderPesquisaAsync(id, dto, Solicitante));
}

// 6. Documentos & Downloads
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentosController(IProntuarioService prontuarioService) : ConsultorioControllerBase
{
    [HttpGet("{id}/arquivo")]
    public async Task<IActionResult> BaixarArquivo(Guid id)
    {
        var res = await prontuarioService.ObterArquivoParaDownloadAsync(id, Solicitante);
        if (res == null) return NotFound();

        return File(res.Value.Bytes, res.Value.ContentType, res.Value.NomeArquivo);
    }
}

// 7. Autorizações de Compartilhamento
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Papeis.Paciente)]
public class AutorizacoesController(IProntuarioService prontuarioService) : ConsultorioControllerBase
{
    [HttpDelete("{id}")]
    public async Task<IActionResult> Revogar(Guid id)
    {
        var ok = await prontuarioService.RevogarAutorizacaoAsync(id, Solicitante);
        return ok ? Ok(new { mensagem = "Autorização revogada com sucesso." }) : NotFound();
    }
}

// 8. Notificações Simuladas
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Papeis.Paciente)]
public class NotificacoesController(INotificacaoPesquisaService service) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await service.ListarNotificacoesPacienteAsync(Solicitante));
}

// 9. Dashboard Operacional
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Papeis.Recepcao)]
public class DashboardController(IDashboardService service) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter() => Ok(await service.ObterDashboardOperacionalAsync(Solicitante.InstituicaoId));
}

// 10. Admin
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Papeis.Admin)]
public class AdminController(IAdminService service) : ConsultorioControllerBase
{
    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios() => Ok(await service.ListarUsuariosAsync());

    [HttpPost("usuarios")]
    public async Task<IActionResult> CriarUsuario([FromBody] CriarUsuarioAdminDto dto)
        => Ok(await service.CriarUsuarioAsync(dto));
}
