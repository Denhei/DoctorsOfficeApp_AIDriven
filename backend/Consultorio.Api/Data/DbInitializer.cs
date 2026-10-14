using Consultorio.Api.Models.Entities;
using Consultorio.Api.Models.Enums;
using Consultorio.Api.Models.Mongo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace Consultorio.Api.Data;

/// <summary>Dados de demonstração: duas clínicas, equipe de cada uma, dois pacientes e o histórico do primeiro.</summary>
public static class DbInitializer
{
    private const string SenhaPadrao = "Senha@123";

    public static async Task SeedAsync(AppDbContext context, UserManager<Usuario> userManager, RoleManager<Papel> roleManager, MongoDbContext mongoContext)
    {
        await context.Database.MigrateAsync();

        foreach (var role in Enum.GetNames<PerfilUsuario>())
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new Papel(role));
            }
        }

        // Se já existem instituições, o seed já rodou.
        if (await context.Instituicoes.AnyAsync())
        {
            return;
        }

        var agora = DateTime.UtcNow;

        var instA = new Instituicao
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Nome = "Clínica Santa Maria",
            RazaoSocial = "Santa Maria Saúde e Diagnósticos Ltda",
        };
        var instB = new Instituicao
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Nome = "Clínica São Bento",
            RazaoSocial = "São Bento Medicina Integrada Ltda",
        };
        context.Instituicoes.AddRange(instA, instB);
        await context.SaveChangesAsync();

        await userManager.CriarUsuarioAsync("admin@consultorio.local", "Administrador do Sistema", SenhaPadrao, PerfilUsuario.Admin,
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        // Equipe da Clínica Santa Maria
        var medAUser = await userManager.CriarUsuarioAsync("medico.santamaria@consultorio.local", "Dr. Carlos Roberto", SenhaPadrao, PerfilUsuario.Medico, instA.Id,
            Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111"));
        var medA = new Medico
        {
            Id = Guid.Parse("cccccccc-1111-1111-1111-111111111111"),
            UsuarioId = medAUser.Id,
            InstituicaoId = instA.Id,
            Especialidade = "Cardiologia",
            CrmFicticio = "CRM/SP 123456"
        };
        var enfAUser = await userManager.CriarUsuarioAsync("enfermeiro.santamaria@consultorio.local", "Enf. Juliana Lima", SenhaPadrao, PerfilUsuario.Enfermeiro, instA.Id,
            Guid.Parse("dddddddd-1111-1111-1111-111111111111"));
        await userManager.CriarUsuarioAsync("atendente.santamaria@consultorio.local", "Atd. Beatriz Alves", SenhaPadrao, PerfilUsuario.Atendente, instA.Id,
            Guid.Parse("eeeeeeee-1111-1111-1111-111111111111"));

        // Equipe da Clínica São Bento
        var medBUser = await userManager.CriarUsuarioAsync("medico.saobento@consultorio.local", "Dra. Ana Paula", SenhaPadrao, PerfilUsuario.Medico, instB.Id,
            Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"));
        var medB = new Medico
        {
            Id = Guid.Parse("cccccccc-2222-2222-2222-222222222222"),
            UsuarioId = medBUser.Id,
            InstituicaoId = instB.Id,
            Especialidade = "Ortopedia",
            CrmFicticio = "CRM/SP 654321"
        };
        await userManager.CriarUsuarioAsync("enfermeiro.saobento@consultorio.local", "Enf. Marcos Dias", SenhaPadrao, PerfilUsuario.Enfermeiro, instB.Id,
            Guid.Parse("dddddddd-2222-2222-2222-222222222222"));
        await userManager.CriarUsuarioAsync("atendente.saobento@consultorio.local", "Atd. Lucas Ferreira", SenhaPadrao, PerfilUsuario.Atendente, instB.Id,
            Guid.Parse("eeeeeeee-2222-2222-2222-222222222222"));

        context.Medicos.AddRange(medA, medB);

        // Pacientes: Carlos tem histórico completo; Mariana é o cenário novo para demonstração ao vivo
        var pacCarlosUser = await userManager.CriarUsuarioAsync("paciente.carlos@consultorio.local", "Carlos Silva", SenhaPadrao, PerfilUsuario.Paciente,
            id: Guid.Parse("ffffffff-1111-1111-1111-111111111111"));
        var pacCarlos = new Paciente
        {
            Id = Guid.Parse("10101010-1111-1111-1111-111111111111"),
            UsuarioId = pacCarlosUser.Id,
            Nome = "Carlos Silva",
            DataNascimento = new DateTime(1985, 5, 20, 0, 0, 0, DateTimeKind.Utc),
            TelefoneFicticio = "(11) 98888-1111",
            EmailFicticio = "carlos.silva@exemplo.local",
            CriadoEmUtc = agora.AddDays(-10)
        };

        var pacMarianaUser = await userManager.CriarUsuarioAsync("paciente.mariana@consultorio.local", "Mariana Souza", SenhaPadrao, PerfilUsuario.Paciente,
            id: Guid.Parse("ffffffff-2222-2222-2222-222222222222"));
        var pacMariana = new Paciente
        {
            Id = Guid.Parse("10101010-2222-2222-2222-222222222222"),
            UsuarioId = pacMarianaUser.Id,
            Nome = "Mariana Souza",
            DataNascimento = new DateTime(1992, 8, 14, 0, 0, 0, DateTimeKind.Utc),
            TelefoneFicticio = "(11) 97777-2222",
            EmailFicticio = "mariana.souza@exemplo.local",
            CriadoEmUtc = agora.AddDays(-2)
        };

        context.Pacientes.AddRange(pacCarlos, pacMariana);
        await context.SaveChangesAsync();

        // Histórico prévio do Carlos: triagem validada, consulta concluída, evolução, exame, autorização e pesquisa
        var coletaCarlos = new ColetaInicial
        {
            Id = Guid.Parse("44444444-1111-1111-1111-111111111111"),
            PacienteId = pacCarlos.Id,
            InstituicaoId = instA.Id,
            Status = StatusColeta.Validada,
            ConversaId = Guid.Parse("33333333-1111-1111-1111-111111111111"),
            ResumoValidado = "Queixa relatada: Dor no peito leve e cansaço. Duração relatada: 5 dias. Alergias relatadas: Nenhuma conhecida. Medicamentos relatados: Losartana 50mg. Observações: Piora aos esforços moderados.",
            EnfermeiroId = enfAUser.Id,
            ValidadoEmUtc = agora.AddDays(-5),
            CriadaEmUtc = agora.AddDays(-5)
        };

        var conversaCarlos = new ConversaMongo
        {
            Id = coletaCarlos.ConversaId.ToString(),
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
            Mensagens =
            [
                MensagemMongo.Bot("Olá, Carlos Silva! Vamos iniciar a coleta guiada. Confirma seus dados?"),
                MensagemMongo.DoPaciente("Sim, confirmo."),
                MensagemMongo.Bot("Qual é a sua queixa principal?"),
                MensagemMongo.DoPaciente("Dor no peito leve e cansaço"),
                MensagemMongo.Bot("Há quanto tempo você sente isso?"),
                MensagemMongo.DoPaciente("5 dias"),
            ],
            CriadaEmUtc = agora.AddDays(-5),
            AtualizadaEmUtc = agora.AddDays(-5)
        };
        await mongoContext.Conversas.ReplaceOneAsync(c => c.Id == conversaCarlos.Id, conversaCarlos, new ReplaceOptions { IsUpsert = true });

        var slotPassado = new SlotAgenda
        {
            Id = Guid.Parse("55555555-1111-1111-1111-111111111111"),
            MedicoId = medA.Id,
            InstituicaoId = instA.Id,
            InicioUtc = agora.AddDays(-4).Date.AddHours(9),
            FimUtc = agora.AddDays(-4).Date.AddHours(9).AddMinutes(30)
        };

        var consultaCarlos = new Consulta
        {
            Id = Guid.Parse("66666666-1111-1111-1111-111111111111"),
            PacienteId = pacCarlos.Id,
            SlotId = slotPassado.Id,
            ColetaId = coletaCarlos.Id,
            Status = StatusConsulta.Concluida,
            CriadaEmUtc = agora.AddDays(-5)
        };

        context.AddRange(
            coletaCarlos,
            slotPassado,
            consultaCarlos,
            new Evolucao
            {
                Id = Guid.Parse("77777777-1111-1111-1111-111111111111"),
                ConsultaId = consultaCarlos.Id,
                MedicoId = medA.Id,
                Texto = "Paciente compareceu com queixa de desconforto torácico. Ausculta cardíaca em ritmo sinusal regular, sem sopros. Solicitado ECG e hemograma completo. Mantida dosagem de Losartana.",
                CriadaEmUtc = slotPassado.InicioUtc.AddMinutes(25)
            },
            new DocumentoPaciente
            {
                Id = Guid.Parse("88888888-1111-1111-1111-111111111111"),
                PacienteId = pacCarlos.Id,
                InstituicaoOrigemId = instA.Id,
                NomeOriginal = "Exame_Eletrocardiograma_Ficticio.pdf",
                Tipo = "application/pdf",
                Extensao = ".pdf",
                TamanhoBytes = 1024 * 15,
                ChaveArquivo = "seed_ecg_carlos.pdf",
                CriadoEmUtc = agora.AddDays(-4)
            },
            new AutorizacaoCompartilhamento
            {
                Id = Guid.Parse("99999999-1111-1111-1111-111111111111"),
                PacienteId = pacCarlos.Id,
                InstituicaoOrigemId = instA.Id,
                InstituicaoDestinoId = instB.Id,
                CriadaEmUtc = agora.AddDays(-3),
            },
            new PesquisaSatisfacao
            {
                Id = Guid.Parse("baaaaaaa-1111-1111-1111-111111111111"),
                ConsultaId = consultaCarlos.Id,
                PacienteId = pacCarlos.Id,
                Nota = 5,
                Percepcao = PercepcaoAtendimento.Melhor,
                Comentario = "Excelente atendimento e triagem rápida.",
                RespondidaEmUtc = agora.AddDays(-4).AddHours(2)
            },
            new NotificacaoSimulada
            {
                PacienteId = pacCarlos.Id,
                ConsultaId = consultaCarlos.Id,
                Canal = CanalNotificacao.WhatsApp,
                Finalidade = FinalidadeNotificacao.ConvitePesquisa,
                Titulo = "Pesquisa de Satisfação - Clínica Santa Maria",
                Texto = "Olá Carlos, sua consulta foi concluída. Por favor, avalie nosso atendimento acessando a pesquisa.",
                CriadaEmUtc = agora.AddDays(-4).AddMinutes(35),
                LidaEmUtc = agora.AddDays(-4).AddHours(2)
            },
            new NotificacaoSimulada
            {
                PacienteId = pacCarlos.Id,
                ConsultaId = consultaCarlos.Id,
                Canal = CanalNotificacao.Email,
                Finalidade = FinalidadeNotificacao.ConvitePesquisa,
                Titulo = "Avalie sua experiência de atendimento",
                Texto = "Prezado Carlos Silva, agradecemos sua visita. Clique aqui para responder ao nosso questionário de qualidade.",
                CriadaEmUtc = agora.AddDays(-4).AddMinutes(35),
                LidaEmUtc = agora.AddDays(-4).AddHours(2)
            });

        // Agenda futura: os 5 dias úteis dos próximos 7 dias, 6 horas cheias com dois slots de 30 min, para cada médico
        int[] horas = [8, 9, 10, 14, 15, 16];
        var slotsFuturos =
            from dia in Enumerable.Range(1, 7)
            let data = agora.Date.AddDays(dia)
            where data.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            from medico in new[] { medA, medB }
            from hora in horas
            from minuto in new[] { 0, 30 }
            let inicio = data.AddHours(hora).AddMinutes(minuto)
            select new SlotAgenda
            {
                MedicoId = medico.Id,
                InstituicaoId = medico.InstituicaoId,
                InicioUtc = inicio,
                FimUtc = inicio.AddMinutes(30)
            };

        context.SlotsAgenda.AddRange(slotsFuturos);
        await context.SaveChangesAsync();
    }
}
