using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

[Collection(ApiCollection.Nome)]
public class AdminTests(ApiFixture api)
{
    [Fact]
    public async Task Listar_usuarios_e_exclusivo_do_admin_e_resolve_pacienteId_e_medicoId()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.ComoAsync(Seed.AtendenteA)).GetAsync("/api/admin/usuarios")).StatusCode);

        var resp = await (await api.ComoAsync(Seed.Admin)).GetAsync("/api/admin/usuarios");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var usuarios = await resp.JsonArray();
        Assert.True(usuarios.Count >= 9, $"seed cria 9 usuários, obteve {usuarios.Count}");

        var medico = Assert.Single(usuarios, u => u.S("email") == Seed.MedicoA);
        Assert.Equal("Medico", medico.S("perfil"));
        Assert.Equal(Seed.MedicoAId, medico.G("medicoId"));
        Assert.Equal("Clínica Santa Maria", medico.S("instituicaoNome"));
        Assert.Null(medico!["pacienteId"]);

        var paciente = Assert.Single(usuarios, u => u.S("email") == Seed.Carlos);
        Assert.Equal(Seed.PacienteCarlos, paciente.G("pacienteId"));
        Assert.Null(paciente!["medicoId"]);
        Assert.Null(paciente["instituicaoId"]);

        var admin = Assert.Single(usuarios, u => u.S("email") == Seed.Admin);
        Assert.Equal("Admin", admin.S("perfil"));
        Assert.Equal("Administrador do Sistema", admin.S("nome"));
    }

    [Fact]
    public async Task Criar_medico_sem_instituicao_retorna_400()
    {
        var admin = await api.ComoAsync(Seed.Admin);
        var resp = await admin.PostAsJsonAsync("/api/admin/usuarios", new
        {
            nomeCompleto = "Dr. Sem Clínica",
            email = $"sem.clinica.{Guid.NewGuid():N}@consultorio.local",
            senha = "Senha@123",
            perfil = "Medico"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("Médico deve ter uma instituição vinculada.", await resp.Mensagem());
    }

    [Fact]
    public async Task Criar_enfermeiro_e_medico_vinculados_a_instituicao()
    {
        var admin = await api.ComoAsync(Seed.Admin);
        var emailEnf = $"enf.{Guid.NewGuid():N}@consultorio.local";

        var enf = await admin.PostAsJsonAsync("/api/admin/usuarios", new
        {
            nomeCompleto = "Enf. Teste",
            email = emailEnf,
            senha = "Senha@123",
            perfil = "Enfermeiro",
            instituicaoId = Seed.InstB
        });
        Assert.Equal(HttpStatusCode.OK, enf.StatusCode);
        var enfJson = await enf.Json();
        Assert.Equal("Enfermeiro", enfJson.S("perfil"));
        Assert.Equal("Clínica São Bento", enfJson.S("instituicaoNome"));
        Assert.Null(enfJson!["medicoId"]);
        Assert.Null(enfJson["pacienteId"]);

        var login = await api.ComoAsync(emailEnf, "Senha@123");
        Assert.Equal(HttpStatusCode.OK, (await login.GetAsync("/api/coletas")).StatusCode);

        var med = await admin.PostAsJsonAsync("/api/admin/usuarios", new
        {
            nomeCompleto = "Dra. Teste",
            email = $"med.{Guid.NewGuid():N}@consultorio.local",
            senha = "Senha@123",
            perfil = "Medico",
            instituicaoId = Seed.InstB,
            especialidadeMedico = "Dermatologia",
            crmMedico = "CRM/SP 999999"
        });
        Assert.Equal(HttpStatusCode.OK, med.StatusCode);
        var medJson = await med.Json();
        Assert.NotNull(medJson!["medicoId"]);

        var medicos = await (await api.Anonimo().GetAsync($"/api/medicos?instituicaoId={Seed.InstB}")).JsonArray();
        var criado = Assert.Single(medicos, m => m.G("id") == medJson.G("medicoId"));
        Assert.Equal("Dermatologia", criado.S("especialidade"));
        Assert.Equal("CRM/SP 999999", criado.S("crmFicticio"));
        Assert.Equal("Dra. Teste", criado.S("nome"));
    }

    [Fact]
    public async Task Criar_usuario_com_email_repetido_retorna_400_com_erro_do_identity()
    {
        var admin = await api.ComoAsync(Seed.Admin);
        var resp = await admin.PostAsJsonAsync("/api/admin/usuarios", new
        {
            nomeCompleto = "Duplicado",
            email = Seed.Carlos,
            senha = "Senha@123",
            perfil = "Atendente",
            instituicaoId = Seed.InstA
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.StartsWith("Erro ao criar usuário:", await resp.Mensagem());
    }
}
