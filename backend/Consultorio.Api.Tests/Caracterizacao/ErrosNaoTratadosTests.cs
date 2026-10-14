using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

/// <summary>
/// Comportamento atual congelado: nestes endpoints o controller não captura
/// <c>UnauthorizedAccessException</c> / <c>InvalidOperationException</c>, e a regra de negócio
/// vaza como 500. Qualquer centralização do tratamento de erros tem de alterar estes
/// testes de forma consciente e documentada.
/// </summary>
[Collection(ApiCollection.Nome)]
public class ErrosNaoTratadosTests(ApiFixture api)
{
    [Fact]
    public async Task Paciente_lendo_cadastro_de_outro_recebe_500()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var resp = await mariana.GetAsync($"/api/pacientes/{Seed.PacienteCarlos}");

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task Paciente_atualizando_cadastro_de_outro_recebe_500()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var resp = await mariana.PatchAsJsonAsync($"/api/pacientes/{Seed.PacienteCarlos}", new { telefoneFicticio = "(11) 90000-0000" });

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task Iniciar_coleta_em_instituicao_inexistente_recebe_500()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var resp = await mariana.PostAsJsonAsync("/api/coletas", new { instituicaoId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [Fact]
    public async Task Criar_paciente_com_email_ja_usado_recebe_500()
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

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }
}
