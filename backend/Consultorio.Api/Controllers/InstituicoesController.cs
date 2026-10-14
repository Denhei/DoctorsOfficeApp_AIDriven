using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstituicoesController(IInstituicaoService service) : ConsultorioControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar() => Ok(await service.ListarInstituicoesAsync());
}
