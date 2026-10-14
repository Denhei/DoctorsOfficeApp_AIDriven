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
