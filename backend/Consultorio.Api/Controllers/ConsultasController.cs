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
public class ConsultasController(
    IAgendaService agendaService,
    IProntuarioService prontuarioService,
    INotificacaoPesquisaService pesquisaService) : ConsultorioControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] Guid? instituicaoId, [FromQuery] Guid? pacienteId)
        => Ok(await agendaService.ListarConsultasAsync(Solicitante, instituicaoId, pacienteId));

    [HttpGet("{id}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var res = await agendaService.ObterConsultaPorIdAsync(id, Solicitante);
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Agendar([FromBody] AgendarConsultaDto dto)
        => Ok(await agendaService.AgendarConsultaAsync(dto, Solicitante));

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusConsultaDto dto)
    {
        var ok = await agendaService.AtualizarStatusConsultaAsync(id, dto.NovoStatus, Solicitante);
        return ok ? Ok(new { mensagem = "Status atualizado com sucesso." }) : BadRequest();
    }

    [HttpPut("{id}/evolucao")]
    [Authorize(Roles = Papeis.Medico)]
    public async Task<IActionResult> RegistrarEvolucao(Guid id, [FromBody] RegistrarEvolucaoDto dto)
        => Ok(await prontuarioService.RegistrarEvolucaoAsync(id, Solicitante, dto));

    [HttpPost("{id}/conclusao")]
    [Authorize(Roles = Papeis.Medico)]
    public async Task<IActionResult> Concluir(Guid id)
        => Ok(await prontuarioService.ConcluirConsultaAsync(id, Solicitante));

    [HttpGet("{id}/pesquisa")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ObterPesquisa(Guid id)
    {
        var res = await pesquisaService.ObterPesquisaConsultaAsync(id, Solicitante);
        return res != null ? Ok(res) : NotFound();
    }

    [HttpPut("{id}/pesquisa")]
    [Authorize(Roles = Papeis.Paciente)]
    public async Task<IActionResult> ResponderPesquisa(Guid id, [FromBody] ResponderPesquisaDto dto)
        => Ok(await pesquisaService.ResponderPesquisaAsync(id, dto, Solicitante));
}
