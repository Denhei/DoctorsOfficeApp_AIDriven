using System.Security.Claims;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

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
public class InstituicoesController : ConsultorioControllerBase
{
    private readonly IInstituicaoService _service;

    public InstituicoesController(IInstituicaoService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarInstituicoesAsync());
}

[ApiController]
[Route("api/[controller]")]
public class MedicosController : ConsultorioControllerBase
{
    private readonly IInstituicaoService _service;

    public MedicosController(IInstituicaoService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromQuery] Guid? instituicaoId) 
        => Ok(await _service.ListarMedicosAsync(instituicaoId));
}

// 2. Pacientes
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PacientesController : ConsultorioControllerBase
{
    private readonly IPacienteService _pacienteService;
    private readonly IProntuarioService _prontuarioService;

    public PacientesController(IPacienteService pacienteService, IProntuarioService prontuarioService)
    {
        _pacienteService = pacienteService;
        _prontuarioService = prontuarioService;
    }

    [HttpGet]
    [Authorize(Roles = "Atendente,Admin,Medico,Enfermeiro")]
    public async Task<IActionResult> Listar() => Ok(await _pacienteService.ListarPacientesAsync());

    [HttpPost]
    [Authorize(Roles = "Atendente,Admin")]
    public async Task<IActionResult> Criar([FromBody] CriarPacienteDto dto) 
        => Ok(await _pacienteService.CriarPacienteAsync(dto));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await _pacienteService.ObterPacientePorIdAsync(id, GetUserId(), GetUserRole());
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarPacienteDto dto)
        => Ok(await _pacienteService.AtualizarPacienteAsync(id, dto, GetUserId(), GetUserRole()));

    [HttpGet("{id}/historico")]
    public async Task<IActionResult> ObterHistorico(Guid id)
    {
        try
        {
            var res = await _prontuarioService.ObterHistoricoPacienteAsync(id, GetUserId(), GetUserRole());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
    }

    [HttpPost("{id}/documentos")]
    public async Task<IActionResult> AnexarDocumento(Guid id, [FromQuery] Guid instituicaoOrigemId, IFormFile arquivo)
    {
        try
        {
            var res = await _prontuarioService.AnexarDocumentoAsync(id, instituicaoOrigemId, arquivo, GetUserId(), GetUserRole());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpGet("{id}/documentos")]
    public async Task<IActionResult> ListarDocumentos(Guid id)
    {
        try
        {
            var res = await _prontuarioService.ListarDocumentosAsync(id, GetUserId(), GetUserRole());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
    }

    [HttpGet("{id}/autorizacoes")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ListarAutorizacoes(Guid id)
    {
        try
        {
            var res = await _prontuarioService.ListarAutorizacoesAsync(id, GetUserId());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
    }

    [HttpPost("{id}/autorizacoes")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ConcederAutorizacao(Guid id, [FromBody] CriarAutorizacaoDto dto)
    {
        try
        {
            var res = await _prontuarioService.ConcederAutorizacaoAsync(id, dto, GetUserId());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }
}

// 3. Coletas & Chatbot Guiado
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ColetasController : ConsultorioControllerBase
{
    private readonly ITriagemChatService _chatService;

    public ColetasController(ITriagemChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Iniciar([FromBody] IniciarColetaDto dto)
    {
        var pacClaim = User.FindFirstValue("pacienteId");
        if (string.IsNullOrEmpty(pacClaim) || !Guid.TryParse(pacClaim, out var pacId))
        {
            return Unauthorized();
        }

        var res = await _chatService.IniciarColetaAsync(pacId, dto.InstituicaoId);
        return Ok(res);
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

        var res = await _chatService.ListarFilaEnfermeiroAsync(instId ?? Guid.Empty);
        return Ok(res);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        try
        {
            var res = await _chatService.ObterConversaPorColetaIdAsync(id, GetUserId(), GetUserRole());
            return res != null ? Ok(res) : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
    }

    [HttpPost("{id}/mensagens")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> EnviarMensagem(Guid id, [FromBody] EnviarMensagemChatDto dto)
    {
        try
        {
            var res = await _chatService.ProcessarMensagemAsync(id, GetUserId(), dto);
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPost("{id}/finalizacao")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> Finalizar(Guid id)
    {
        try
        {
            var res = await _chatService.FinalizarEnvioEnfermeiroAsync(id, GetUserId());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPut("{id}/validacao")]
    [Authorize(Roles = "Enfermeiro")]
    public async Task<IActionResult> Validar(Guid id, [FromBody] ValidarColetaDto dto)
    {
        var sucesso = await _chatService.ValidarColetaAsync(id, GetUserId(), dto);
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
public class SlotsController : ConsultorioControllerBase
{
    private readonly IAgendaService _agendaService;

    public SlotsController(IAgendaService agendaService)
    {
        _agendaService = agendaService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromQuery] Guid? medicoId, [FromQuery] Guid? instituicaoId, [FromQuery] DateTime? data)
        => Ok(await _agendaService.ListarSlotsAsync(medicoId, instituicaoId, data));

    [HttpPost]
    [Authorize(Roles = "Atendente,Admin")]
    public async Task<IActionResult> Criar([FromBody] CriarSlotDto dto)
    {
        var instId = GetInstituicaoId() ?? dto.InstituicaoId;
        try
        {
            var res = await _agendaService.CriarSlotAsync(dto, instId);
            return Ok(res);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }
}

// 5. Consultas
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConsultasController : ConsultorioControllerBase
{
    private readonly IAgendaService _agendaService;
    private readonly IProntuarioService _prontuarioService;
    private readonly INotificacaoPesquisaService _pesquisaService;

    public ConsultasController(
        IAgendaService agendaService, 
        IProntuarioService prontuarioService, 
        INotificacaoPesquisaService pesquisaService)
    {
        _agendaService = agendaService;
        _prontuarioService = prontuarioService;
        _pesquisaService = pesquisaService;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? instituicaoId, [FromQuery] Guid? pacienteId, [FromQuery] Guid? medicoId)
        => Ok(await _agendaService.ListarConsultasAsync(GetUserId(), GetUserRole(), instituicaoId, pacienteId, medicoId));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        try
        {
            var res = await _agendaService.ObterConsultaPorIdAsync(id, GetUserId(), GetUserRole());
            return res != null ? Ok(res) : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Agendar([FromBody] AgendarConsultaDto dto)
    {
        try
        {
            var res = await _agendaService.AgendarConsultaAsync(dto, GetUserId(), GetUserRole());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("já foi reservado") || ex.Message.Contains("mesmo intervalo"))
            {
                return StatusCode(StatusCodes.Status409Conflict, new { mensagem = ex.Message });
            }
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusConsultaDto dto)
    {
        try
        {
            var ok = await _agendaService.AtualizarStatusConsultaAsync(id, dto.NovoStatus, GetUserId(), GetUserRole());
            return ok ? Ok(new { mensagem = "Status atualizado com sucesso." }) : BadRequest();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPut("{id}/evolucao")]
    [Authorize(Roles = "Medico")]
    public async Task<IActionResult> RegistrarEvolucao(Guid id, [FromBody] RegistrarEvolucaoDto dto)
    {
        try
        {
            var res = await _prontuarioService.RegistrarEvolucaoAsync(id, GetUserId(), dto);
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPost("{id}/conclusao")]
    [Authorize(Roles = "Medico")]
    public async Task<IActionResult> Concluir(Guid id)
    {
        try
        {
            var res = await _prontuarioService.ConcluirConsultaAsync(id, GetUserId());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpGet("{id}/pesquisa")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ObterPesquisa(Guid id)
    {
        var res = await _pesquisaService.ObterPesquisaConsultaAsync(id, GetUserId());
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPut("{id}/pesquisa")]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> ResponderPesquisa(Guid id, [FromBody] ResponderPesquisaDto dto)
    {
        try
        {
            var res = await _pesquisaService.ResponderPesquisaAsync(id, dto, GetUserId());
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }
}

// 6. Documentos & Downloads
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentosController : ConsultorioControllerBase
{
    private readonly IProntuarioService _prontuarioService;

    public DocumentosController(IProntuarioService prontuarioService)
    {
        _prontuarioService = prontuarioService;
    }

    [HttpGet("{id}/arquivo")]
    public async Task<IActionResult> BaixarArquivo(Guid id)
    {
        try
        {
            var res = await _prontuarioService.ObterArquivoParaDownloadAsync(id, GetUserId(), GetUserRole());
            if (res == null) return NotFound();

            return File(res.Value.Bytes, res.Value.ContentType, res.Value.NomeArquivo);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
    }
}

// 7. Autorizações de Compartilhamento
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Paciente")]
public class AutorizacoesController : ConsultorioControllerBase
{
    private readonly IProntuarioService _prontuarioService;

    public AutorizacoesController(IProntuarioService prontuarioService)
    {
        _prontuarioService = prontuarioService;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Revogar(Guid id)
    {
        try
        {
            var ok = await _prontuarioService.RevogarAutorizacaoAsync(id, GetUserId());
            return ok ? Ok(new { mensagem = "Autorização revogada com sucesso." }) : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message });
        }
    }
}

// 8. Notificações Simuladas
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Paciente")]
public class NotificacoesController : ConsultorioControllerBase
{
    private readonly INotificacaoPesquisaService _service;

    public NotificacoesController(INotificacaoPesquisaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarNotificacoesPacienteAsync(GetUserId()));
}

// 9. Dashboard Operacional
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Atendente,Admin")]
public class DashboardController : ConsultorioControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Obter() => Ok(await _service.ObterDashboardOperacionalAsync(GetInstituicaoId()));
}

// 10. Admin
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ConsultorioControllerBase
{
    private readonly IAdminService _service;

    public AdminController(IAdminService service)
    {
        _service = service;
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios() => Ok(await _service.ListarUsuariosAsync());

    [HttpPost("usuarios")]
    public async Task<IActionResult> CriarUsuario([FromBody] CriarUsuarioAdminDto dto)
    {
        try
        {
            return Ok(await _service.CriarUsuarioAsync(dto));
        }
        catch (Exception ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }
}
