using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

[Collection(ApiCollection.Nome)]
public class AuthTests(ApiFixture api)
{
    [Fact]
    public async Task Login_de_paciente_retorna_token_perfil_e_pacienteId()
    {
        var resp = await api.Anonimo().PostAsJsonAsync("/api/auth/login", new { email = Seed.Mariana, senha = Seed.Senha });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Json();
        Assert.False(string.IsNullOrWhiteSpace(json.S("token")));
        Assert.True(json.D("expiraEmUtc") > DateTime.UtcNow.AddHours(23));
        var usuario = json["usuario"];
        Assert.Equal("Paciente", usuario.S("perfil"));
        Assert.Equal("Mariana Souza", usuario.S("nome"));
        Assert.Equal(Seed.PacienteMariana, usuario.G("pacienteId"));
        Assert.Null(usuario!["medicoId"]);
        Assert.Null(usuario["instituicaoId"]);
    }

    [Fact]
    public async Task Login_de_medico_inclui_medicoId_e_instituicao()
    {
        var resp = await api.Anonimo().PostAsJsonAsync("/api/auth/login", new { email = Seed.MedicoA, senha = Seed.Senha });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var usuario = (await resp.Json())["usuario"];
        Assert.Equal("Medico", usuario.S("perfil"));
        Assert.Equal(Seed.MedicoAId, usuario.G("medicoId"));
        Assert.Equal(Seed.InstA, usuario.G("instituicaoId"));
        Assert.Equal("Clínica Santa Maria", usuario.S("instituicaoNome"));
        Assert.Null(usuario!["pacienteId"]);
    }

    [Fact]
    public async Task Login_com_senha_errada_retorna_401_com_mensagem()
    {
        var resp = await api.Anonimo().PostAsJsonAsync("/api/auth/login", new { email = Seed.Mariana, senha = "errada" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal("Credenciais inválidas ou usuário não encontrado.", await resp.Mensagem());
    }

    [Fact]
    public async Task Login_de_usuario_inexistente_retorna_401()
    {
        var resp = await api.Anonimo().PostAsJsonAsync("/api/auth/login", new { email = "ninguem@x.local", senha = Seed.Senha });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_sem_token_retorna_401()
    {
        var resp = await api.Anonimo().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_com_token_retorna_dados_do_usuario()
    {
        var client = await api.ComoAsync(Seed.EnfermeiroB);
        var resp = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Json();
        Assert.Equal("Enfermeiro", json.S("perfil"));
        Assert.Equal("Enf. Marcos Dias", json.S("nome"));
        Assert.Equal(Seed.InstB, json.G("instituicaoId"));
        Assert.Equal("Clínica São Bento", json.S("instituicaoNome"));
    }
}
