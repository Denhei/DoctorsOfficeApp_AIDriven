using Consultorio.Api.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Consultorio.Api.Data;

public class AppDbContext : IdentityDbContext<Usuario, Papel, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Instituicao> Instituicoes => Set<Instituicao>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Medico> Medicos => Set<Medico>();
    public DbSet<SlotAgenda> SlotsAgenda => Set<SlotAgenda>();
    public DbSet<ColetaInicial> ColetasIniciais => Set<ColetaInicial>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<Evolucao> Evolucoes => Set<Evolucao>();
    public DbSet<DocumentoPaciente> DocumentosPaciente => Set<DocumentoPaciente>();
    public DbSet<AutorizacaoCompartilhamento> AutorizacoesCompartilhamento => Set<AutorizacaoCompartilhamento>();
    public DbSet<PesquisaSatisfacao> PesquisasSatisfacao => Set<PesquisaSatisfacao>();
    public DbSet<NotificacaoSimulada> NotificacoesSimuladas => Set<NotificacaoSimulada>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Renomear tabelas do Identity para padrão limpo
        builder.Entity<Usuario>().ToTable("Usuarios");
        builder.Entity<Papel>().ToTable("Papeis");

        // Índices únicos 1:1
        builder.Entity<Paciente>()
            .HasIndex(p => p.UsuarioId)
            .IsUnique();

        builder.Entity<Medico>()
            .HasIndex(m => m.UsuarioId)
            .IsUnique();

        builder.Entity<Evolucao>()
            .HasIndex(e => e.ConsultaId)
            .IsUnique();

        builder.Entity<PesquisaSatisfacao>()
            .HasIndex(p => p.ConsultaId)
            .IsUnique();

        // Constraint Parcial: Apenas 1 consulta NÃO cancelada por Slot
        builder.Entity<Consulta>()
            .HasIndex(c => c.SlotId)
            .HasFilter("\"Status\" != 4") // StatusConsulta.Cancelada = 4
            .IsUnique();

        // Relacionamentos e Deleções
        builder.Entity<SlotAgenda>()
            .HasOne(s => s.Instituicao)
            .WithMany(i => i.Slots)
            .HasForeignKey(s => s.InstituicaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SlotAgenda>()
            .HasOne(s => s.Medico)
            .WithMany(m => m.Slots)
            .HasForeignKey(s => s.MedicoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ColetaInicial>()
            .HasOne(c => c.Paciente)
            .WithMany(p => p.Coletas)
            .HasForeignKey(c => c.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ColetaInicial>()
            .HasOne(c => c.Instituicao)
            .WithMany()
            .HasForeignKey(c => c.InstituicaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Consulta>()
            .HasOne(c => c.Paciente)
            .WithMany(p => p.Consultas)
            .HasForeignKey(c => c.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Consulta>()
            .HasOne(c => c.Slot)
            .WithMany(s => s.Consultas)
            .HasForeignKey(c => c.SlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Consulta>()
            .HasOne(c => c.Coleta)
            .WithMany(co => co.Consultas)
            .HasForeignKey(c => c.ColetaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AutorizacaoCompartilhamento>()
            .HasOne(a => a.Paciente)
            .WithMany(p => p.Autorizacoes)
            .HasForeignKey(a => a.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AutorizacaoCompartilhamento>()
            .HasOne(a => a.InstituicaoOrigem)
            .WithMany()
            .HasForeignKey(a => a.InstituicaoOrigemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AutorizacaoCompartilhamento>()
            .HasOne(a => a.InstituicaoDestino)
            .WithMany()
            .HasForeignKey(a => a.InstituicaoDestinoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DocumentoPaciente>()
            .HasOne(d => d.Paciente)
            .WithMany(p => p.Documentos)
            .HasForeignKey(d => d.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DocumentoPaciente>()
            .HasOne(d => d.InstituicaoOrigem)
            .WithMany()
            .HasForeignKey(d => d.InstituicaoOrigemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<NotificacaoSimulada>()
            .HasOne(n => n.Paciente)
            .WithMany()
            .HasForeignKey(n => n.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
