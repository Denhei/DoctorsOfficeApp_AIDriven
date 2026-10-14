using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Models.Mongo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace Consultorio.Api.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, UserManager<Usuario> userManager, RoleManager<Papel> roleManager, MongoDbContext mongoContext)
    {
        // 1. Garantir que as migrations do PostgreSQL foram aplicadas
        await context.Database.MigrateAsync();

        // 2. Criar Papéis do Identity
        string[] roles = Enum.GetNames<PerfilUsuario>();
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new Papel(role));
            }
        }

        // Se já existem instituições, assumimos que o seed já rodou
        if (await context.Instituicoes.AnyAsync())
        {
            return;
        }

        var senhaPadrao = "Senha@123";

        // 3. Criar Instituições
        var instA = new Instituicao
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Nome = "Clínica Santa Maria",
            RazaoSocial = "Santa Maria Saúde e Diagnósticos Ltda",
            Ativa = true,
            CriadoEmUtc = DateTime.UtcNow
        };

        var instB = new Instituicao
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Nome = "Clínica São Bento",
            RazaoSocial = "São Bento Medicina Integrada Ltda",
            Ativa = true,
            CriadoEmUtc = DateTime.UtcNow
        };

        await context.Instituicoes.AddRangeAsync(instA, instB);
        await context.SaveChangesAsync();

        // 4. Criar Admin Técnico
        var adminUser = new Usuario
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserName = "admin@consultorio.local",
            Email = "admin@consultorio.local",
            NomeCompleto = "Administrador do Sistema",
            Perfil = PerfilUsuario.Admin,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(adminUser, senhaPadrao);
        await userManager.AddToRoleAsync(adminUser, PerfilUsuario.Admin.ToString());

        // 5. Profissionais da Clínica Santa Maria
        var medAUser = new Usuario
        {
            Id = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111"),
            UserName = "medico.santamaria@consultorio.local",
            Email = "medico.santamaria@consultorio.local",
            NomeCompleto = "Dr. Carlos Roberto",
            Perfil = PerfilUsuario.Medico,
            InstituicaoId = instA.Id,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(medAUser, senhaPadrao);
        await userManager.AddToRoleAsync(medAUser, PerfilUsuario.Medico.ToString());

        var medA = new Medico
        {
            Id = Guid.Parse("cccccccc-1111-1111-1111-111111111111"),
            UsuarioId = medAUser.Id,
            InstituicaoId = instA.Id,
            Especialidade = "Cardiologia",
            CrmFicticio = "CRM/SP 123456"
        };
        await context.Medicos.AddAsync(medA);

        var enfAUser = new Usuario
        {
            Id = Guid.Parse("dddddddd-1111-1111-1111-111111111111"),
            UserName = "enfermeiro.santamaria@consultorio.local",
            Email = "enfermeiro.santamaria@consultorio.local",
            NomeCompleto = "Enf. Juliana Lima",
            Perfil = PerfilUsuario.Enfermeiro,
            InstituicaoId = instA.Id,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(enfAUser, senhaPadrao);
        await userManager.AddToRoleAsync(enfAUser, PerfilUsuario.Enfermeiro.ToString());

        var atdAUser = new Usuario
        {
            Id = Guid.Parse("eeeeeeee-1111-1111-1111-111111111111"),
            UserName = "atendente.santamaria@consultorio.local",
            Email = "atendente.santamaria@consultorio.local",
            NomeCompleto = "Atd. Beatriz Alves",
            Perfil = PerfilUsuario.Atendente,
            InstituicaoId = instA.Id,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(atdAUser, senhaPadrao);
        await userManager.AddToRoleAsync(atdAUser, PerfilUsuario.Atendente.ToString());

        // 6. Profissionais da Clínica São Bento
        var medBUser = new Usuario
        {
            Id = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"),
            UserName = "medico.saobento@consultorio.local",
            Email = "medico.saobento@consultorio.local",
            NomeCompleto = "Dra. Ana Paula",
            Perfil = PerfilUsuario.Medico,
            InstituicaoId = instB.Id,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(medBUser, senhaPadrao);
        await userManager.AddToRoleAsync(medBUser, PerfilUsuario.Medico.ToString());

        var medB = new Medico
        {
            Id = Guid.Parse("cccccccc-2222-2222-2222-222222222222"),
            UsuarioId = medBUser.Id,
            InstituicaoId = instB.Id,
            Especialidade = "Ortopedia",
            CrmFicticio = "CRM/SP 654321"
        };
        await context.Medicos.AddAsync(medB);

        var enfBUser = new Usuario
        {
            Id = Guid.Parse("dddddddd-2222-2222-2222-222222222222"),
            UserName = "enfermeiro.saobento@consultorio.local",
            Email = "enfermeiro.saobento@consultorio.local",
            NomeCompleto = "Enf. Marcos Dias",
            Perfil = PerfilUsuario.Enfermeiro,
            InstituicaoId = instB.Id,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(enfBUser, senhaPadrao);
        await userManager.AddToRoleAsync(enfBUser, PerfilUsuario.Enfermeiro.ToString());

        var atdBUser = new Usuario
        {
            Id = Guid.Parse("eeeeeeee-2222-2222-2222-222222222222"),
            UserName = "atendente.saobento@consultorio.local",
            Email = "atendente.saobento@consultorio.local",
            NomeCompleto = "Atd. Lucas Ferreira",
            Perfil = PerfilUsuario.Atendente,
            InstituicaoId = instB.Id,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(atdBUser, senhaPadrao);
        await userManager.AddToRoleAsync(atdBUser, PerfilUsuario.Atendente.ToString());

        // 7. Paciente 1 (Carlos Silva - Cenário com Histórico Completo)
        var pacCarlosUser = new Usuario
        {
            Id = Guid.Parse("ffffffff-1111-1111-1111-111111111111"),
            UserName = "paciente.carlos@consultorio.local",
            Email = "paciente.carlos@consultorio.local",
            NomeCompleto = "Carlos Silva",
            Perfil = PerfilUsuario.Paciente,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(pacCarlosUser, senhaPadrao);
        await userManager.AddToRoleAsync(pacCarlosUser, PerfilUsuario.Paciente.ToString());

        var pacCarlos = new Paciente
        {
            Id = Guid.Parse("10101010-1111-1111-1111-111111111111"),
            UsuarioId = pacCarlosUser.Id,
            Nome = "Carlos Silva",
            DataNascimento = new DateTime(1985, 5, 20, 0, 0, 0, DateTimeKind.Utc),
            TelefoneFicticio = "(11) 98888-1111",
            EmailFicticio = "carlos.silva@exemplo.local",
            CriadoEmUtc = DateTime.UtcNow.AddDays(-10)
        };
        await context.Pacientes.AddAsync(pacCarlos);

        // 8. Paciente 2 (Mariana Souza - Cenário Novo para Demonstração ao Vivo)
        var pacMarianaUser = new Usuario
        {
            Id = Guid.Parse("ffffffff-2222-2222-2222-222222222222"),
            UserName = "paciente.mariana@consultorio.local",
            Email = "paciente.mariana@consultorio.local",
            NomeCompleto = "Mariana Souza",
            Perfil = PerfilUsuario.Paciente,
            EmailConfirmed = true,
            CriadoEmUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(pacMarianaUser, senhaPadrao);
        await userManager.AddToRoleAsync(pacMarianaUser, PerfilUsuario.Paciente.ToString());

        var pacMariana = new Paciente
        {
            Id = Guid.Parse("10101010-2222-2222-2222-222222222222"),
            UsuarioId = pacMarianaUser.Id,
            Nome = "Mariana Souza",
            DataNascimento = new DateTime(1992, 8, 14, 0, 0, 0, DateTimeKind.Utc),
            TelefoneFicticio = "(11) 97777-2222",
            EmailFicticio = "mariana.souza@exemplo.local",
            CriadoEmUtc = DateTime.UtcNow.AddDays(-2)
        };
        await context.Pacientes.AddAsync(pacMariana);
        await context.SaveChangesAsync();

        // 9. Histórico Prévio do Paciente 1 (Carlos Silva)
        var conversaCarlosId = Guid.Parse("33333333-1111-1111-1111-111111111111");
        var coletaCarlos = new ColetaInicial
        {
            Id = Guid.Parse("44444444-1111-1111-1111-111111111111"),
            PacienteId = pacCarlos.Id,
            InstituicaoId = instA.Id,
            Status = StatusColeta.Validada,
            ConversaId = conversaCarlosId,
            ResumoValidado = "Queixa relatada: Dor no peito leve e cansaço. Duração relatada: 5 dias. Alergias relatadas: Nenhuma conhecida. Medicamentos relatados: Losartana 50mg. Observações: Piora aos esforços moderados.",
            EnfermeiroId = enfAUser.Id,
            ValidadoEmUtc = DateTime.UtcNow.AddDays(-5),
            CriadaEmUtc = DateTime.UtcNow.AddDays(-5)
        };
        await context.ColetasIniciais.AddAsync(coletaCarlos);

        // Inserir conversa do Carlos no MongoDB
        var conversaCarlosMongo = new ConversaMongo
        {
            Id = conversaCarlosId.ToString(),
            ColetaId = coletaCarlos.Id.ToString(),
            PacienteId = pacCarlos.Id.ToString(),
            InstituicaoId = instA.Id.ToString(),
            EtapaAtual = 8,
            ResumoPreliminar = new ResumoPreliminarMongo
            {
                Queixa = "Dor no peito leve e cansaço",
                Duracao = "5 dias",
                Alergias = "Nenhuma conhecida",
                Medicamentos = "Losartana 50mg",
                Observacoes = "Piora aos esforços"
            },
            Mensagens = new List<MensagemMongo>
            {
                new() { Remetente = "Bot", Conteudo = "Olá, Carlos Silva! Vamos iniciar a coleta guiada. Confirma seus dados?", Tipo = "Texto" },
                new() { Remetente = "Paciente", Conteudo = "Sim, confirmo.", Tipo = "Texto" },
                new() { Remetente = "Bot", Conteudo = "Qual é a sua queixa principal?", Tipo = "Texto" },
                new() { Remetente = "Paciente", Conteudo = "Dor no peito leve e cansaço", Tipo = "Texto" },
                new() { Remetente = "Bot", Conteudo = "Há quanto tempo você sente isso?", Tipo = "Texto" },
                new() { Remetente = "Paciente", Conteudo = "5 dias", Tipo = "Texto" }
            },
            CriadaEmUtc = DateTime.UtcNow.AddDays(-5),
            AtualizadaEmUtc = DateTime.UtcNow.AddDays(-5)
        };
        await mongoContext.Conversas.ReplaceOneAsync(
            c => c.Id == conversaCarlosMongo.Id,
            conversaCarlosMongo,
            new ReplaceOptions { IsUpsert = true }
        );

        var slotPassado = new SlotAgenda
        {
            Id = Guid.Parse("55555555-1111-1111-1111-111111111111"),
            MedicoId = medA.Id,
            InstituicaoId = instA.Id,
            InicioUtc = DateTime.UtcNow.AddDays(-4).Date.AddHours(9),
            FimUtc = DateTime.UtcNow.AddDays(-4).Date.AddHours(9).AddMinutes(30)
        };
        await context.SlotsAgenda.AddAsync(slotPassado);

        var consultaCarlos = new Consulta
        {
            Id = Guid.Parse("66666666-1111-1111-1111-111111111111"),
            PacienteId = pacCarlos.Id,
            SlotId = slotPassado.Id,
            ColetaId = coletaCarlos.Id,
            Status = StatusConsulta.Concluida,
            CriadaEmUtc = DateTime.UtcNow.AddDays(-5)
        };
        await context.Consultas.AddAsync(consultaCarlos);

        var evolucaoCarlos = new Evolucao
        {
            Id = Guid.Parse("77777777-1111-1111-1111-111111111111"),
            ConsultaId = consultaCarlos.Id,
            MedicoId = medA.Id,
            Texto = "Paciente compareceu com queixa de desconforto torácico. Ausculta cardíaca em ritmo sinusal regular, sem sopros. Solicitado ECG e hemograma completo. Mantida dosagem de Losartana.",
            CriadaEmUtc = DateTime.UtcNow.AddDays(-4).Date.AddHours(9).AddMinutes(25)
        };
        await context.Evolucoes.AddAsync(evolucaoCarlos);

        var docCarlos = new DocumentoPaciente
        {
            Id = Guid.Parse("88888888-1111-1111-1111-111111111111"),
            PacienteId = pacCarlos.Id,
            InstituicaoOrigemId = instA.Id,
            NomeOriginal = "Exame_Eletrocardiograma_Ficticio.pdf",
            Tipo = "application/pdf",
            Extensao = ".pdf",
            TamanhoBytes = 1024 * 15,
            ChaveArquivo = "seed_ecg_carlos.pdf",
            CriadoEmUtc = DateTime.UtcNow.AddDays(-4)
        };
        await context.DocumentosPaciente.AddAsync(docCarlos);

        var autorizacaoCarlos = new AutorizacaoCompartilhamento
        {
            Id = Guid.Parse("99999999-1111-1111-1111-111111111111"),
            PacienteId = pacCarlos.Id,
            InstituicaoOrigemId = instA.Id,
            InstituicaoDestinoId = instB.Id,
            CriadaEmUtc = DateTime.UtcNow.AddDays(-3),
            RevogadaEmUtc = null // Ativa
        };
        await context.AutorizacoesCompartilhamento.AddAsync(autorizacaoCarlos);

        var pesquisaCarlos = new PesquisaSatisfacao
        {
            Id = Guid.Parse("baaaaaaa-1111-1111-1111-111111111111"),
            ConsultaId = consultaCarlos.Id,
            PacienteId = pacCarlos.Id,
            Nota = 5,
            Percepcao = PercepcaoAtendimento.Melhor,
            Comentario = "Excelente atendimento e triagem rápida.",
            RespondidaEmUtc = DateTime.UtcNow.AddDays(-4).AddHours(2)
        };
        await context.PesquisasSatisfacao.AddAsync(pesquisaCarlos);

        var notif1 = new NotificacaoSimulada
        {
            Id = Guid.NewGuid(),
            PacienteId = pacCarlos.Id,
            ConsultaId = consultaCarlos.Id,
            Canal = CanalNotificacao.WhatsApp,
            Finalidade = FinalidadeNotificacao.ConvitePesquisa,
            Titulo = "Pesquisa de Satisfação - Clínica Santa Maria",
            Texto = "Olá Carlos, sua consulta foi concluída. Por favor, avalie nosso atendimento acessando a pesquisa.",
            CriadaEmUtc = DateTime.UtcNow.AddDays(-4).AddMinutes(35),
            LidaEmUtc = DateTime.UtcNow.AddDays(-4).AddHours(2)
        };
        var notif2 = new NotificacaoSimulada
        {
            Id = Guid.NewGuid(),
            PacienteId = pacCarlos.Id,
            ConsultaId = consultaCarlos.Id,
            Canal = CanalNotificacao.Email,
            Finalidade = FinalidadeNotificacao.ConvitePesquisa,
            Titulo = "Avalie sua experiência de atendimento",
            Texto = "Prezado Carlos Silva, agradecemos sua visita. Clique aqui para responder ao nosso questionário de qualidade.",
            CriadaEmUtc = DateTime.UtcNow.AddDays(-4).AddMinutes(35),
            LidaEmUtc = DateTime.UtcNow.AddDays(-4).AddHours(2)
        };
        await context.NotificacoesSimuladas.AddRangeAsync(notif1, notif2);

        // 10. Gerar Slots Futuros (próximos 7 dias úteis) para ambos os médicos
        var agora = DateTime.UtcNow.Date;
        var slotsFuturos = new List<SlotAgenda>();

        for (int dia = 1; dia <= 7; dia++)
        {
            var dataSlot = agora.AddDays(dia);
            if (dataSlot.DayOfWeek == DayOfWeek.Saturday || dataSlot.DayOfWeek == DayOfWeek.Sunday)
                continue;

            // Slots Dr. Carlos (Santa Maria) - 08:00 às 11:00
            int[] horas = { 8, 9, 10, 14, 15, 16 };
            foreach (var hora in horas)
            {
                slotsFuturos.Add(new SlotAgenda
                {
                    Id = Guid.NewGuid(),
                    MedicoId = medA.Id,
                    InstituicaoId = instA.Id,
                    InicioUtc = dataSlot.AddHours(hora),
                    FimUtc = dataSlot.AddHours(hora).AddMinutes(30)
                });

                slotsFuturos.Add(new SlotAgenda
                {
                    Id = Guid.NewGuid(),
                    MedicoId = medA.Id,
                    InstituicaoId = instA.Id,
                    InicioUtc = dataSlot.AddHours(hora).AddMinutes(30),
                    FimUtc = dataSlot.AddHours(hora + 1)
                });
            }

            // Slots Dra. Ana Paula (São Bento) - 09:00 às 17:00
            foreach (var hora in horas)
            {
                slotsFuturos.Add(new SlotAgenda
                {
                    Id = Guid.NewGuid(),
                    MedicoId = medB.Id,
                    InstituicaoId = instB.Id,
                    InicioUtc = dataSlot.AddHours(hora),
                    FimUtc = dataSlot.AddHours(hora).AddMinutes(30)
                });

                slotsFuturos.Add(new SlotAgenda
                {
                    Id = Guid.NewGuid(),
                    MedicoId = medB.Id,
                    InstituicaoId = instB.Id,
                    InicioUtc = dataSlot.AddHours(hora).AddMinutes(30),
                    FimUtc = dataSlot.AddHours(hora + 1)
                });
            }
        }

        await context.SlotsAgenda.AddRangeAsync(slotsFuturos);
        await context.SaveChangesAsync();
    }
}
