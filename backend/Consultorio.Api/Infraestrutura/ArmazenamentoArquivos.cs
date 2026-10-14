using Microsoft.Extensions.Options;

namespace Consultorio.Api.Infraestrutura;

/// <summary>Arquivos de pacientes em disco local. Singleton: o diretório é garantido uma vez, na subida.</summary>
public sealed class ArmazenamentoArquivos
{
    private readonly string _diretorio;

    public ArmazenamentoArquivos(IOptions<StorageOptions> opcoes)
    {
        _diretorio = opcoes.Value.UploadDirectory;
        Directory.CreateDirectory(_diretorio);
    }

    /// <returns>Chave única sob a qual o arquivo foi gravado.</returns>
    public async Task<string> SalvarAsync(IFormFile arquivo, string extensao)
    {
        var chave = $"{Guid.NewGuid()}{extensao}";
        await using var stream = File.Create(Path.Combine(_diretorio, chave));
        await arquivo.CopyToAsync(stream);
        return chave;
    }

    /// <returns>Conteúdo do arquivo, ou null quando a chave não existe em disco.</returns>
    public async Task<byte[]?> LerAsync(string chave)
    {
        var caminho = Path.Combine(_diretorio, chave);
        return File.Exists(caminho) ? await File.ReadAllBytesAsync(caminho) : null;
    }
}
