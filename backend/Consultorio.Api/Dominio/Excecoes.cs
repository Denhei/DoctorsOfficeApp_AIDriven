namespace Consultorio.Api.Dominio;

/// <summary>Regra de negócio violada. O tratador global responde HTTP 400 com a mensagem.</summary>
public class RegraDeNegocioException(string mensagem) : Exception(mensagem);

/// <summary>Disputa de recurso (slot ou horário já tomado). HTTP 409.</summary>
public sealed class ConflitoException(string mensagem) : RegraDeNegocioException(mensagem);

/// <summary>O solicitante não pode agir sobre este recurso. HTTP 403.</summary>
public sealed class AcessoNegadoException(string mensagem) : Exception(mensagem);
