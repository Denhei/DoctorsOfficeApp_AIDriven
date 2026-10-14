using System.Security.Claims;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

// Exceções de regra (InvalidOperationException, ConflitoException) e de autorização
// (UnauthorizedAccessException) são traduzidas para HTTP por TratadorGlobalDeExcecoes.
public abstract class ConsultorioControllerBase : ControllerBase
{
    protected Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    protected PerfilUsuario GetUserRole()
    {
        var claim = User.FindFirstValue(ClaimTypes.Role) 
            ?? User.FindFirstValue("perfil")
            ?? User.FindFirstValue("role")
            ?? User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        return Enum.TryParse<PerfilUsuario>(claim, out var role) ? role : PerfilUsuario.Paciente;
    }

    protected Guid? GetInstituicaoId()
    {
        var claim = User.FindFirstValue("instituicaoId");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
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
    [Authorize(Roles = "Atendente,Admin,Medico,Enfermeiro")]
    public async Task<IActionResult> Listar() => Ok(await pacienteService.ListarPacientesAsync());

    [HttpPost]
    [Authorize(Roles = "Atendente,Admin")]
    public async Task<IActionResult> Criar([FromBody] CriarPacienteDto dto) 
        => Ok(await pacienteService.CriarPacienteAsync(dto));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await pacienteService.ObterPacientePorIdAsync(id, GetUserId(), GetUserRole());
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarPacienteDto dto)
        => Ok(await pacienteService.AtualizarPacienteAsync(id, dto, GetUserId(), GetUserRole()));

    [HttpGet("{id}/historico")]
    public async Task<IActionResult> ObterHistorico(Guid id)
        => Ok(await prontuarioService.ObterHistoricoPacienteAsync(id, GetUserId(), GetUserRole()));

    [HttpPost("{id}/documentos")]
    public async Task<IActionResult> AnexarDocumento(Guid id, [FromQuery] Guid instituicaoOrigemId, IFormFile arquivo)
        => Ok(await prontuarioService.AnexarDocumentoAsync(id, instituicaoOrigemId, arquivo, GetUserId(), GetUserRole()));

    [HttpGet("{id}/documentos")]
    public async Task<IActionResult> ListarDocumentos(Guid id)
        => Ok(await prontuarioService.ListarDocumentosAsync(id, GetUserId(), GetUserRole()));

    [HttpGet("{id}/autorizacoes")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ListarAutorizacoes(Guid id)
        => Ok(await prontuarioService.ListarAutorizacoesAsync(id, GetUserId()));

    [HttpPost("{id}/autorizacoes")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ConcederAutorizacao(Guid id, [FromBody] CriarAutorizacaoDto dto)
        => Ok(await prontuarioService.ConcederAutorizacaoAsync(id, dto, GetUserId()));
}

// 3. Coletas & Chatbot Guiado
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ColetasController(ITriagemChatService chatService) : ConsultorioControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Iniciar([FromBody] IniciarColetaDto dto)
    {
        var pacClaim = User.FindFirstValue("pacienteId");
        if (string.IsNullOrEmpty(pacClaim) || !Guid.TryParse(pacClaim, out var pacId))
        {
            return Unauthorized();
        }

        return Ok(await chatService.IniciarColetaAsync(pacId, dto.InstituicaoId));
    }

    [HttpGet]
    [Authorize(Roles = "Enfermeiro,Admin")]
    public async Task<IActionResult> ListarFila([FromQuery] string? status)
    {
        var instId = GetInstituicaoId();
        if (!instId.HasValue && GetUserRole() != PerfilUsuario.Admin)
        {
            return Forbid();
        }

        return Ok(await chatService.ListarFilaEnfermeiroAsync(instId ?? Guid.Empty));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await chatService.ObterConversaPorColetaIdAsync(id, GetUserId(), GetUserRole());
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPost("{id}/mensagens")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> EnviarMensagem(Guid id, [FromBody] EnviarMensagemChatDto dto)
        => Ok(await chatService.ProcessarMensagemAsync(id, GetUserId(), dto));

    [HttpPost("{id}/finalizacao")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Finalizar(Guid id)
        => Ok(await chatService.FinalizarEnvioEnfermeiroAsync(id, GetUserId()));

    [HttpPut("{id}/validacao")]
    [Authorize(Roles = "Enfermeiro")]
    public async Task<IActionResult> Validar(Guid id, [FromBody] ValidarColetaDto dto)
    {
        var sucesso = await chatService.ValidarColetaAsync(id, GetUserId(), dto);
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
    [Authorize(Roles = "Atendente,Admin")]
    public async Task<IActionResult> Criar([FromBody] CriarSlotDto dto)
        => Ok(await agendaService.CriarSlotAsync(dto, GetInstituicaoId() ?? dto.InstituicaoId));
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
    public async Task<IActionResult> Listar([FromQuery] Guid? instituicaoId, [FromQuery] Guid? pacienteId, [FromQuery] Guid? medicoId)
        => Ok(await agendaService.ListarConsultasAsync(GetUserId(), GetUserRole(), instituicaoId, pacienteId, medicoId));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await agendaService.ObterConsultaPorIdAsync(id, GetUserId(), GetUserRole());
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Agendar([FromBody] AgendarConsultaDto dto)
        => Ok(await agendaService.AgendarConsultaAsync(dto, GetUserId(), GetUserRole()));

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusConsultaDto dto)
    {
        var ok = await agendaService.AtualizarStatusConsultaAsync(id, dto.NovoStatus, GetUserId(), GetUserRole());
        return ok ? Ok(new { mensagem = "Status atualizado com sucesso." }) : BadRequest();
    }

    [HttpPut("{id}/evolucao")]
    [Authorize(Roles = "Medico")]
    public async Task<IActionResult> RegistrarEvolucao(Guid id, [FromBody] RegistrarEvolucaoDto dto)
        => Ok(await prontuarioService.RegistrarEvolucaoAsync(id, GetUserId(), dto));

    [HttpPost("{id}/conclusao")]
    [Authorize(Roles = "Medico")]
    public async Task<IActionResult> Concluir(Guid id)
        => Ok(await prontuarioService.ConcluirConsultaAsync(id, GetUserId()));

    [HttpGet("{id}/pesquisa")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ObterPesquisa(Guid id)
    {
        var res = await pesquisaService.ObterPesquisaConsultaAsync(id, GetUserId());
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPut("{id}/pesquisa")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ResponderPesquisa(Guid id, [FromBody] ResponderPesquisaDto dto)
        => Ok(await pesquisaService.ResponderPesquisaAsync(id, dto, GetUserId()));
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
        var res = await prontuarioService.ObterArquivoParaDownloadAsync(id, GetUserId(), GetUserRole());
        if (res == null) return NotFound();

        return File(res.Value.Bytes, res.Value.ContentType, res.Value.NomeArquivo);
    }
}

// 7. Autorizações de Compartilhamento
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Paciente")]
public class AutorizacoesController(IProntuarioService prontuarioService) : ConsultorioControllerBase
{
    [HttpDelete("{id}")]
    public async Task<IActionResult> Revogar(Guid id)
    {
        var ok = await prontuarioService.RevogarAutorizacaoAsync(id, GetUserId());
        return ok ? Ok(new { mensagem = "Autorização revogada com sucesso." }) : NotFound();
    }
}

// 8. Notificações Simuladas
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Paciente")]
public class NotificacoesController(INotificacaoPesquisaService service) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await service.ListarNotificacoesPacienteAsync(GetUserId()));
}

// 9. Dashboard Operacional
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Atendente,Admin")]
public class DashboardController(IDashboardService service) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter() => Ok(await service.ObterDashboardOperacionalAsync(GetInstituicaoId()));
}

// 10. Admin
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(IAdminService service) : ConsultorioControllerBase
{
    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios() => Ok(await service.ListarUsuariosAsync());

    [HttpPost("usuarios")]
    public async Task<IActionResult> CriarUsuario([FromBody] CriarUsuarioAdminDto dto)
        => Ok(await service.CriarUsuarioAsync(dto));
}
