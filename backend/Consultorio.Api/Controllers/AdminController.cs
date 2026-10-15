using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

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
