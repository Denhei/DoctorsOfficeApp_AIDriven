using Consultorio.Api.Dominio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Consultorio.Api.Infraestrutura;

/// <summary>
/// Único ponto que traduz as exceções de domínio lançadas pelas ações em resposta HTTP
/// <c>{ "mensagem": "..." }</c>. Vale só dentro do MVC, então falhas de infraestrutura
/// (configuração, disco, banco) continuam sendo 500 e são registradas como erro.
/// </summary>
public sealed class TratadorGlobalDeExcecoes : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        int? status = contexto.Exception switch
        {
            ConflitoException => StatusCodes.Status409Conflict,
            RegraDeNegocioException => StatusCodes.Status400BadRequest,
            AcessoNegadoException => StatusCodes.Status403Forbidden,
            _ => null,
        };

        if (status is null) return;

        contexto.Result = new ObjectResult(new { mensagem = contexto.Exception.Message }) { StatusCode = status };
        contexto.ExceptionHandled = true;
    }
}
