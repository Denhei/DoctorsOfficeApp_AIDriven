using Consultorio.Api.DTOs;
using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Consultorio.Api.Controllers;

// RegraDeNegocioException, ConflitoException e AcessoNegadoException viram { mensagem } no TratadorGlobalDeExcecoes.
public abstract class ConsultorioControllerBase : ControllerBase
{
    private UsuarioAtual? _solicitante;

    protected UsuarioAtual Solicitante => _solicitante ??= UsuarioAtual.De(User);
}
