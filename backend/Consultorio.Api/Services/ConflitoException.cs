namespace Consultorio.Api.Services;

/// <summary>
/// Regra de negócio violada por disputa de recurso (slot ou horário já tomado).
/// O tratador global responde HTTP 409; as demais InvalidOperationException viram 400.
/// </summary>
public sealed class ConflitoException(string mensagem) : InvalidOperationException(mensagem);
