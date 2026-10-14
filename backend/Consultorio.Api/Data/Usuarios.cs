using Consultorio.Api.Dominio;
using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace Consultorio.Api.Data;

public static class Usuarios
{
    /// <summary>
    /// Projeção única de Usuario para UsuarioInfoDto. PacienteId e MedicoId são resolvidos no próprio SQL,
    /// o que elimina a consulta extra por usuário (N+1) que existia em cada chamador.
    /// </summary>
    public static IQueryable<UsuarioInfoDto> ProjetarInfo(this IQueryable<Usuario> usuarios, AppDbContext db) =>
        usuarios.Select(u => new UsuarioInfoDto(
            u.Id,
            u.NomeCompleto,
            u.Email ?? string.Empty,
            u.Perfil,
            u.InstituicaoId,
            u.Instituicao != null ? u.Instituicao.Nome : null,
            db.Pacientes.Where(p => p.UsuarioId == u.Id).Select(p => (Guid?)p.Id).FirstOrDefault(),
            db.Medicos.Where(m => m.UsuarioId == u.Id).Select(m => (Guid?)m.Id).FirstOrDefault()));

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
