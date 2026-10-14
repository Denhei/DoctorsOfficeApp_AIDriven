using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

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
