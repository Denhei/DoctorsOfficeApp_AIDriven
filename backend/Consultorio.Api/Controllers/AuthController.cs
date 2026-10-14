using Consultorio.Api.DTOs;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ConsultorioControllerBase
{
    /// <summary>
    /// Realiza login no sistema com contas fictícias pré-carregadas.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var response = await authService.LoginAsync(dto);
        if (response == null)
        {
            return Unauthorized(new { mensagem = "Credenciais inválidas ou usuário não encontrado." });
        }

        return Ok(response);
    }

    /// <summary>
    /// Retorna os dados e perfil do usuário autenticado no momento.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var userInfo = await authService.ObterUsuarioAtualAsync(userId);
        if (userInfo == null)
        {
            return NotFound(new { mensagem = "Usuário não encontrado." });
        }

        return Ok(userInfo);
    }
}
