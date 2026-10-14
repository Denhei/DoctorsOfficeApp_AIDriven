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
public class DocumentosController(IProntuarioService prontuarioService) : ConsultorioControllerBase
{
    [HttpGet("{id}/arquivo")]
    public async Task<IActionResult> BaixarArquivo(Guid id)
    {
        var res = await prontuarioService.ObterArquivoParaDownloadAsync(id, Solicitante);
        if (res == null) return NotFound();

        return File(res.Value.Bytes, res.Value.ContentType, res.Value.NomeArquivo);
    }
}
