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
public class PacientesController(IPacienteService pacienteService, IProntuarioService prontuarioService) : ConsultorioControllerBase
{
    [HttpGet]
    [Authorize(Roles = Papeis.Equipe)]
    public async Task<IActionResult> Listar() => Ok(await pacienteService.ListarPacientesAsync());

    [HttpPost]
    [Authorize(Roles = Papeis.Recepcao)]
    public async Task<IActionResult> Criar([FromBody] CriarPacienteDto dto) 
        => Ok(await pacienteService.CriarPacienteAsync(dto));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await pacienteService.ObterPacientePorIdAsync(id, Solicitante);
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarPacienteDto dto)
        => Ok(await pacienteService.AtualizarPacienteAsync(id, dto, Solicitante));

    [HttpGet("{id}/historico")]
    public async Task<IActionResult> ObterHistorico(Guid id)
        => Ok(await prontuarioService.ObterHistoricoPacienteAsync(id, Solicitante));

    [HttpPost("{id}/documentos")]
    public async Task<IActionResult> AnexarDocumento(Guid id, [FromQuery] Guid instituicaoOrigemId, IFormFile arquivo)
        => Ok(await prontuarioService.AnexarDocumentoAsync(id, instituicaoOrigemId, arquivo, Solicitante));

    [HttpGet("{id}/documentos")]
    public async Task<IActionResult> ListarDocumentos(Guid id)
        => Ok(await prontuarioService.ListarDocumentosAsync(id, Solicitante));

    [HttpGet("{id}/autorizacoes")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ListarAutorizacoes(Guid id)
        => Ok(await prontuarioService.ListarAutorizacoesAsync(id, Solicitante));

    [HttpPost("{id}/autorizacoes")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ConcederAutorizacao(Guid id, [FromBody] CriarAutorizacaoDto dto)
        => Ok(await prontuarioService.ConcederAutorizacaoAsync(id, dto, Solicitante));
}
