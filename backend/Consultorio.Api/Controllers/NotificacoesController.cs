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
public class NotificacoesController(INotificacaoPesquisaService service) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await service.ListarNotificacoesPacienteAsync(Solicitante));
}
