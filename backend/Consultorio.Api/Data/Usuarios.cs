using Consultorio.Api.Dominio;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace Consultorio.Api.Data;

public static class Usuarios
{
    /// <summary>Cria o usuário do Identity já confirmado e no papel do seu perfil.</summary>
    public static async Task<Usuario> CriarUsuarioAsync(
        this UserManager<Usuario> userManager,
        string email,
        string nomeCompleto,
        string senha,
        PerfilUsuario perfil,
        Guid? instituicaoId = null,
        Guid? id = null)
    {
        var usuario = new Usuario
        {
            Id = id ?? Guid.NewGuid(),
            UserName = email,
            Email = email,
            NomeCompleto = nomeCompleto,
            Perfil = perfil,
            InstituicaoId = instituicaoId,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };

        var resultado = await userManager.CreateAsync(usuario, senha);
        if (!resultado.Succeeded)
        {
            var erros = string.Join(", ", resultado.Errors.Select(e => e.Description));
            throw new RegraDeNegocioException($"Erro ao criar usuário: {erros}");
        }

        await userManager.AddToRoleAsync(usuario, perfil.ToString());
        return usuario;
    }
}
