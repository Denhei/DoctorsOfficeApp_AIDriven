using System.ComponentModel.DataAnnotations;

namespace Consultorio.Api.Infraestrutura;

/// <summary>Seção "Jwt" do appsettings. Validada na subida: sem chave de 32+ bytes a API não inicia.</summary>
public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    [Required, MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 24 * 30)]
    public int ExpirationHours { get; init; } = 24;
}

/// <summary>Seção "Storage" do appsettings.</summary>
public sealed class StorageOptions
{
    public const string Secao = "Storage";

    public string UploadDirectory { get; init; } = "./uploads";
}
