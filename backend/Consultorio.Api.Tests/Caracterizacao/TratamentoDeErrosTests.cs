using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

/// <summary>
/// Mudança consciente da refatoração: o baseline original congelava estes quatro casos como 500,
/// porque o controller não capturava UnauthorizedAccessException / InvalidOperationException
/// (ver histórico deste arquivo, antes chamado ErrosNaoTratadosTests). Com o tratador global,
/// toda regra de negócio responde 400 e toda negação de acesso responde 403, com corpo { mensagem }.
/// </summary>
[Collection(ApiCollection.Nome)]
public class TratamentoDeErrosTests(ApiFixture api)
{
    [Fact]
    public async Task Paciente_lendo_cadastro_de_outro_recebe_403()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var resp = await mariana.GetAsync($"/api/pacientes/{Seed.PacienteCarlos}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("Acesso negado aos dados de outro paciente.", await resp.Mensagem());
    }

    [Fact]
    public async Task Paciente_atualizando_cadastro_de_outro_recebe_403()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var resp = await mariana.PatchAsJsonAsync($"/api/pacientes/{Seed.PacienteCarlos}", new { telefoneFicticio = "(11) 90000-0000" });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("Acesso negado.", await resp.Mensagem());
    }

    [Fact]
    public async Task Iniciar_coleta_em_instituicao_inexistente_recebe_400()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var resp = await mariana.PostAsJsonAsync("/api/coletas", new { instituicaoId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("Instituição não encontrada.", await resp.Mensagem());
    }

    [Fact]
    public async Task Criar_paciente_com_email_ja_usado_recebe_400()
    {
        var atendente = await api.ComoAsync(Seed.AtendenteA);
        var resp = await atendente.PostAsJsonAsync("/api/pacientes", new
        {
            nome = "Repetido",
            dataNascimento = "1990-01-01T00:00:00",
            telefoneFicticio = "(11) 90000-0000",
            emailFicticio = Seed.Carlos,
            senha = "Senha@123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.StartsWith("Erro ao criar usuário:", await resp.Mensagem());
    }
}

[Collection(ApiCollection.Nome)]
public class CorsEmErrosTests(ApiFixture api)
{
    [Fact]
    public async Task Resposta_de_erro_de_negocio_mantem_cabecalho_cors_para_o_frontend()
    {
        var atendente = await api.ComoAsync(Seed.AtendenteA);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/pacientes/{Seed.PacienteCarlos}/historico");
        req.Headers.Add("Origin", "http://localhost:4200");

        var resp = await atendente.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("http://localhost:4200", Assert.Single(resp.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("Atendentes e Administradores Técnicos não possuem permissão de acesso ao histórico clínico.", await resp.Mensagem());
    }
}
