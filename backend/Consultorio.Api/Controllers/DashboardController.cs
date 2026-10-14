using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Papeis.Recepcao)]
public class DashboardController(IDashboardService service) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter() => Ok(await service.ObterDashboardOperacionalAsync(Solicitante.InstituicaoId));
}
