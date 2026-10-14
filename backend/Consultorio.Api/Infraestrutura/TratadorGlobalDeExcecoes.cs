using Consultorio.Api.Services;
using Microsoft.AspNetCore.Diagnostics;

namespace Consultorio.Api.Infraestrutura;

/// <summary>
/// Único ponto que traduz exceções de regra e de autorização em resposta HTTP
/// no formato <c>{ "mensagem": "..." }</c>. Outras exceções seguem para o pipeline padrão (500).
/// </summary>
public sealed class TratadorGlobalDeExcecoes : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        int? status = excecao switch
        {
            ConflitoException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => null,
        };

        if (status is null) return false;

        http.Response.StatusCode = status.Value;
        await http.Response.WriteAsJsonAsync(new { mensagem = excecao.Message }, ct);
        return true;
    }
}
