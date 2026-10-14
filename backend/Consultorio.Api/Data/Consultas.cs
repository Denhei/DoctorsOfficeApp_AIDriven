using Consultorio.Api.DTOs;
using Consultorio.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Data;

public static class Consultas
{
    /// <summary>Carrega tudo que <see cref="Mapeamentos.ToDto(Consulta)"/> lê.</summary>
    public static IQueryable<Consulta> ComDetalhes(this IQueryable<Consulta> consultas) => consultas
        .Include(c => c.Paciente)
        .Include(c => c.Coleta)
        .Include(c => c.Evolucao)
        .Include(c => c.PesquisaSatisfacao)
        .Include(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)
        .Include(c => c.Slot).ThenInclude(s => s.Instituicao);
}
