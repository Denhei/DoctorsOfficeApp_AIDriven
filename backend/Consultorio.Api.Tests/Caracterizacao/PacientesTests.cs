using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

[Collection(ApiCollection.Nome)]
public class PacientesTests(ApiFixture api)
{
    [Fact]
    public async Task Listar_pacientes_e_restrito_a_equipe()
    {
        var comoPaciente = await (await api.ComoAsync(Seed.Carlos)).GetAsync("/api/pacientes");
        Assert.Equal(HttpStatusCode.Forbidden, comoPaciente.StatusCode);

        var comoAtendente = await (await api.ComoAsync(Seed.AtendenteB)).GetAsync("/api/pacientes");
        Assert.Equal(HttpStatusCode.OK, comoAtendente.StatusCode);
        var lista = await comoAtendente.JsonArray();
        Assert.True(lista.Count >= 2);
        Assert.Contains(lista, p => p.G("id") == Seed.PacienteCarlos && p.S("nome") == "Carlos Silva" && p.S("telefoneFicticio") == "(11) 98888-1111");
        Assert.Contains(lista, p => p.G("id") == Seed.PacienteMariana && p.S("emailFicticio") == "mariana.souza@exemplo.local");
    }

    [Fact]
    public async Task Atendente_cria_paciente_que_consegue_logar()
    {
        var atendente = await api.ComoAsync(Seed.AtendenteA);
        var email = $"novo.{Guid.NewGuid():N}@exemplo.local";

        var resp = await atendente.PostAsJsonAsync("/api/pacientes", new
        {
            nome = "Paciente Novo",
            dataNascimento = "1990-03-15T00:00:00",
            telefoneFicticio = "(11) 90000-1234",
            emailFicticio = email,
            senha = "Senha@123"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var criado = await resp.Json();
        Assert.Equal("Paciente Novo", criado.S("nome"));
        Assert.Equal(email, criado.S("emailFicticio"));
        Assert.Equal(new DateTime(1990, 3, 15), criado.D("dataNascimento").Date);

        var login = await api.ComoAsync(email, "Senha@123");
        var me = await (await login.GetAsync("/api/auth/me")).Json();
        Assert.Equal("Paciente", me.S("perfil"));
        Assert.Equal(criado.G("id"), me.G("pacienteId"));
    }

    [Fact]
    public async Task Paciente_le_e_atualiza_apenas_o_proprio_cadastro()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);

        var proprio = await mariana.GetAsync($"/api/pacientes/{Seed.PacienteMariana}");
        Assert.Equal(HttpStatusCode.OK, proprio.StatusCode);
        Assert.Equal("Mariana Souza", (await proprio.Json()).S("nome"));

        var inexistente = await mariana.GetAsync($"/api/pacientes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        var patch = await mariana.PatchAsJsonAsync($"/api/pacientes/{Seed.PacienteMariana}", new { telefoneFicticio = "(11) 95555-2222" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var atualizado = await patch.Json();
        Assert.Equal("(11) 95555-2222", atualizado.S("telefoneFicticio"));
        Assert.Equal("mariana.souza@exemplo.local", atualizado.S("emailFicticio"));

        var patchVazio = await mariana.PatchAsJsonAsync($"/api/pacientes/{Seed.PacienteMariana}", new { telefoneFicticio = "   " });
        Assert.Equal("(11) 95555-2222", (await patchVazio.Json()).S("telefoneFicticio"));
    }

    [Fact]
    public async Task Equipe_le_qualquer_paciente()
    {
        var medicoB = await api.ComoAsync(Seed.MedicoB);
        var resp = await medicoB.GetAsync($"/api/pacientes/{Seed.PacienteCarlos}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Carlos Silva", (await resp.Json()).S("nome"));
    }
}
