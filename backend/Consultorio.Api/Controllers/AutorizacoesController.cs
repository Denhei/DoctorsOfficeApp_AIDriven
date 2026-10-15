using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

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
