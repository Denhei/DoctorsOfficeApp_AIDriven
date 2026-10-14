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

/// <summary>
/// Usuários com perfil mas sem o registro correspondente (o admin cria usuário Paciente sem Paciente,
/// e cria o usuário Medico antes de recusar a falta de instituição). Dois destes casos respondiam 500
/// no original (notificações e pesquisa); os demais preservam o comportamento.
/// </summary>
[Collection(ApiCollection.Nome)]
public class UsuariosSemRegistroTests(ApiFixture api)
{
    [Fact]
    public async Task Paciente_sem_registro_de_paciente()
    {
        var admin = await api.ComoAsync(Seed.Admin);
        var email = $"paciente.sem.registro.{Guid.NewGuid():N}@consultorio.local";
        var criado = await admin.PostAsJsonAsync("/api/admin/usuarios", new { nomeCompleto = "Sem Registro", email, senha = "Senha@123", perfil = "Paciente" });
        Assert.Equal(HttpStatusCode.OK, criado.StatusCode);
        Assert.Null((await criado.Json())!["pacienteId"]);

        var usuario = await api.ComoAsync(email, "Senha@123");

        Assert.Equal(HttpStatusCode.Unauthorized, (await usuario.PostAsJsonAsync("/api/coletas", new { instituicaoId = Seed.InstA })).StatusCode);

        var notificacoes = await usuario.GetAsync("/api/notificacoes");
        Assert.Equal(HttpStatusCode.Forbidden, notificacoes.StatusCode);
        Assert.Equal("Paciente não encontrado.", await notificacoes.Mensagem());

        var pesquisa = await usuario.GetAsync($"/api/consultas/{Seed.ConsultaCarlos}/pesquisa");
        Assert.Equal(HttpStatusCode.Forbidden, pesquisa.StatusCode);
        Assert.Equal("Paciente não encontrado.", await pesquisa.Mensagem());

        var consultas = await usuario.GetAsync("/api/consultas");
        Assert.Equal(HttpStatusCode.OK, consultas.StatusCode);
        Assert.Empty(await consultas.JsonArray());
    }

    [Fact]
    public async Task Medico_sem_registro_de_medico()
    {
        var admin = await api.ComoAsync(Seed.Admin);
        var email = $"medico.sem.registro.{Guid.NewGuid():N}@consultorio.local";
        var criado = await admin.PostAsJsonAsync("/api/admin/usuarios", new { nomeCompleto = "Dr. Sem Registro", email, senha = "Senha@123", perfil = "Medico" });
        Assert.Equal(HttpStatusCode.BadRequest, criado.StatusCode);

        var medico = await api.ComoAsync(email, "Senha@123");

        var historico = await medico.GetAsync($"/api/pacientes/{Seed.PacienteCarlos}/historico");
        Assert.Equal(HttpStatusCode.Forbidden, historico.StatusCode);
        Assert.Equal("Médico não encontrado.", await historico.Mensagem());

        var download = await medico.GetAsync($"/api/documentos/{Seed.DocumentoCarlos}/arquivo");
        Assert.Equal(HttpStatusCode.Forbidden, download.StatusCode);
        Assert.Equal("Médico não encontrado.", await download.Mensagem());

        var evolucao = await medico.PutAsJsonAsync($"/api/consultas/{Seed.ConsultaCarlos}/evolucao", new { texto = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, evolucao.StatusCode);
        Assert.Equal("Usuário não é um médico cadastrado.", await evolucao.Mensagem());

        var conclusao = await medico.PostAsync($"/api/consultas/{Seed.ConsultaCarlos}/conclusao", null);
        Assert.Equal(HttpStatusCode.Forbidden, conclusao.StatusCode);
        Assert.Equal("Usuário não é um médico.", await conclusao.Mensagem());

        var porId = await medico.GetAsync($"/api/consultas/{Seed.ConsultaCarlos}");
        Assert.Equal(HttpStatusCode.Forbidden, porId.StatusCode);
        Assert.Equal("Acesso negado.", await porId.Mensagem());

        Assert.Empty(await (await medico.GetAsync("/api/consultas")).JsonArray());
    }

    [Fact]
    public async Task Enfermeiro_sem_instituicao_nao_ve_fila()
    {
        var admin = await api.ComoAsync(Seed.Admin);
        var email = $"enf.sem.inst.{Guid.NewGuid():N}@consultorio.local";
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/admin/usuarios", new { nomeCompleto = "Enf. Sem Instituição", email, senha = "Senha@123", perfil = "Enfermeiro" })).StatusCode);

        var enfermeiro = await api.ComoAsync(email, "Senha@123");
        var fila = await enfermeiro.GetAsync("/api/coletas");

        Assert.Equal(HttpStatusCode.Forbidden, fila.StatusCode);
        Assert.Equal(0, fila.Content.Headers.ContentLength ?? 0);
    }
}

/// <summary>Paciente inexistente: as três rotas respondiam 500 no original e agora 400 { mensagem }.</summary>
[Collection(ApiCollection.Nome)]
public class PacienteInexistenteTests(ApiFixture api)
{
    [Fact]
    public async Task Historico_documentos_e_autorizacoes_de_paciente_inexistente_retornam_400()
    {
        var carlos = await api.ComoAsync(Seed.Carlos);
        var inexistente = Guid.NewGuid();

        foreach (var rota in new[] { "historico", "documentos", "autorizacoes" })
        {
            var resp = await carlos.GetAsync($"/api/pacientes/{inexistente}/{rota}");
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            Assert.Equal("Paciente não encontrado.", await resp.Mensagem());
        }

        var conceder = await carlos.PostAsJsonAsync($"/api/pacientes/{inexistente}/autorizacoes", new { instituicaoOrigemId = Seed.InstA, instituicaoDestinoId = Seed.InstB });
        Assert.Equal(HttpStatusCode.BadRequest, conceder.StatusCode);
        Assert.Equal("Paciente não encontrado.", await conceder.Mensagem());

        var atualizar = await (await api.ComoAsync(Seed.AtendenteA)).PatchAsJsonAsync($"/api/pacientes/{inexistente}", new { telefoneFicticio = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, atualizar.StatusCode);
        Assert.Equal("Paciente não encontrado.", await atualizar.Mensagem());
    }
}
