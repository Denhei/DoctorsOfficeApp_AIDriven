using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

[Collection(ApiCollection.Nome)]
public class ProntuarioTests(ApiFixture api)
{
    [Fact]
    public async Task Historico_do_proprio_paciente_traz_consulta_evolucao_documento_e_autorizacao_do_seed()
    {
        var carlos = await api.ComoAsync(Seed.Carlos);
        var resp = await carlos.GetAsync($"/api/pacientes/{Seed.PacienteCarlos}/historico");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var hist = await resp.Json();
        Assert.Equal("Carlos Silva", hist.S("nome"));
        Assert.Equal(new DateTime(1985, 5, 20), hist.D("dataNascimento").Date);

        var consulta = Assert.Single(hist["consultas"]!.AsArray());
        Assert.Equal(Seed.ConsultaCarlos, consulta.G("id"));
        Assert.Equal("Concluida", consulta.S("status"));
        Assert.Equal("Dr. Carlos Roberto", consulta.S("medicoNome"));
        Assert.Equal("Cardiologia", consulta.S("especialidade"));
        Assert.Equal("Clínica Santa Maria", consulta.S("instituicaoNome"));
        Assert.Equal(Seed.ColetaCarlos, consulta.G("coletaId"));
        Assert.StartsWith("Queixa relatada: Dor no peito", consulta.S("resumoValidado"));
        Assert.True(consulta.B("temEvolucao"));
        // Comportamento atual: o histórico não inclui PesquisaSatisfacao na consulta, logo reporta false
        // mesmo com a pesquisa do seed respondida (GET /api/consultas/{id} reporta true).
        Assert.False(consulta.B("temPesquisaRespondida"));

        var evolucao = Assert.Single(hist["evolucoes"]!.AsArray());
        Assert.Equal(Seed.ConsultaCarlos, evolucao.G("consultaId"));
        Assert.Equal("Dr. Carlos Roberto", evolucao.S("medicoNome"));
        Assert.StartsWith("Paciente compareceu com queixa", evolucao.S("texto"));

        var doc = Assert.Single(hist["documentos"]!.AsArray());
        Assert.Equal(Seed.DocumentoCarlos, doc.G("id"));
        Assert.Equal("Exame_Eletrocardiograma_Ficticio.pdf", doc.S("nomeOriginal"));
        Assert.Equal("Clínica Santa Maria", doc.S("instituicaoOrigemNome"));
        Assert.Equal(15 * 1024, doc!["tamanhoBytes"]!.GetValue<long>());

        var aut = Assert.Single(hist["autorizacoesAtivas"]!.AsArray());
        Assert.Equal(Seed.AutorizacaoCarlos, aut.G("id"));
        Assert.Equal("Clínica Santa Maria", aut.S("instituicaoOrigemNome"));
        Assert.Equal("Clínica São Bento", aut.S("instituicaoDestinoNome"));
        Assert.True(aut.B("ativa"));
    }

    [Fact]
    public async Task Historico_e_negado_a_atendente_admin_outro_paciente_e_medico_sem_vinculo()
    {
        var url = $"/api/pacientes/{Seed.PacienteCarlos}/historico";

        var atendente = await (await api.ComoAsync(Seed.AtendenteA)).GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, atendente.StatusCode);
        Assert.Equal("Atendentes e Administradores Técnicos não possuem permissão de acesso ao histórico clínico.", await atendente.Mensagem());

        var admin = await (await api.ComoAsync(Seed.Admin)).GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);

        var outroPaciente = await (await api.ComoAsync(Seed.Mariana)).GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, outroPaciente.StatusCode);
        Assert.Equal("Você não tem permissão para acessar o histórico de outro paciente.", await outroPaciente.Mensagem());

        var medicoSemConsulta = await (await api.ComoAsync(Seed.MedicoB)).GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, medicoSemConsulta.StatusCode);
        Assert.Equal("Médico só pode acessar o histórico de pacientes com consulta vinculada sob sua responsabilidade.", await medicoSemConsulta.Mensagem());
    }

    [Fact]
    public async Task Medico_responsavel_ve_historico_da_propria_instituicao()
    {
        var medicoA = await api.ComoAsync(Seed.MedicoA);
        var resp = await medicoA.GetAsync($"/api/pacientes/{Seed.PacienteCarlos}/historico");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var hist = await resp.Json();
        Assert.Single(hist["consultas"]!.AsArray());
        Assert.Single(hist["documentos"]!.AsArray());
        Assert.Single(hist["evolucoes"]!.AsArray());
    }

    [Fact]
    public async Task Upload_de_documento_valida_extensao_e_permite_download_ao_dono()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var url = $"/api/pacientes/{Seed.PacienteMariana}/documentos?instituicaoOrigemId={Seed.InstA}";

        using var txt = Multipart("laudo.txt", "text/plain", "conteudo");
        var invalido = await mariana.PostAsync(url, txt);
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Equal("Formato de arquivo não suportado. Utilize PDF, JPG ou PNG.", await invalido.Mensagem());

        var bytes = "%PDF-1.4 conteudo ficticio"u8.ToArray();
        using var pdf = Multipart("Laudo Mariana.pdf", "application/pdf", bytes);
        var ok = await mariana.PostAsync(url, pdf);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var doc = await ok.Json();
        Assert.Equal("Laudo Mariana.pdf", doc.S("nomeOriginal"));
        Assert.Equal(".pdf", doc.S("extensao"));
        Assert.Equal("application/pdf", doc.S("tipo"));
        Assert.Equal(bytes.Length, doc["tamanhoBytes"]!.GetValue<long>());
        Assert.Equal("Clínica Santa Maria", doc.S("instituicaoOrigemNome"));

        var lista = await (await mariana.GetAsync($"/api/pacientes/{Seed.PacienteMariana}/documentos")).JsonArray();
        Assert.Contains(lista, d => d.G("id") == doc.G("id"));

        var download = await mariana.GetAsync($"/api/documentos/{doc.G("id")}/arquivo");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());

        var atendente = await (await api.ComoAsync(Seed.AtendenteA)).GetAsync($"/api/documentos/{doc.G("id")}/arquivo");
        Assert.Equal(HttpStatusCode.Forbidden, atendente.StatusCode);
        Assert.Equal("Atendentes e Administradores não possuem permissão para download de arquivos clínicos.", await atendente.Mensagem());

        var outroPaciente = await (await api.ComoAsync(Seed.Carlos)).GetAsync($"/api/documentos/{doc.G("id")}/arquivo");
        Assert.Equal(HttpStatusCode.Forbidden, outroPaciente.StatusCode);
    }

    [Fact]
    public async Task Upload_por_atendente_e_negado()
    {
        var atendente = await api.ComoAsync(Seed.AtendenteA);
        using var pdf = Multipart("x.pdf", "application/pdf", "x");
        var resp = await atendente.PostAsync($"/api/pacientes/{Seed.PacienteMariana}/documentos?instituicaoOrigemId={Seed.InstA}", pdf);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("Apenas o próprio paciente ou seu médico podem anexar documentos.", await resp.Mensagem());
    }

    [Fact]
    public async Task Download_de_documento_do_seed_sem_arquivo_em_disco_gera_conteudo_ficticio()
    {
        var carlos = await api.ComoAsync(Seed.Carlos);
        var resp = await carlos.GetAsync($"/api/documentos/{Seed.DocumentoCarlos}/arquivo");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var texto = await resp.Content.ReadAsStringAsync();
        Assert.StartsWith("[Arquivo Fictício de Demonstração]: Exame_Eletrocardiograma_Ficticio.pdf", texto);

        var inexistente = await carlos.GetAsync($"/api/documentos/{Guid.NewGuid()}/arquivo");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Medico_de_outra_instituicao_baixa_documento_apenas_com_autorizacao_e_consulta()
    {
        // Dra. Ana Paula (São Bento) não tem consulta com Carlos: negado mesmo com autorização ativa.
        var medicoB = await api.ComoAsync(Seed.MedicoB);
        var resp = await medicoB.GetAsync($"/api/documentos/{Seed.DocumentoCarlos}/arquivo");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("Acesso negado.", await resp.Mensagem());
    }

    [Fact]
    public async Task Autorizacoes_concessao_listagem_e_revogacao_pelo_proprio_paciente()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var url = $"/api/pacientes/{Seed.PacienteMariana}/autorizacoes";

        var mesmaInstituicao = await mariana.PostAsJsonAsync(url, new { instituicaoOrigemId = Seed.InstA, instituicaoDestinoId = Seed.InstA });
        Assert.Equal(HttpStatusCode.BadRequest, mesmaInstituicao.StatusCode);
        Assert.Equal("Instituição de origem e destino devem ser diferentes.", await mesmaInstituicao.Mensagem());

        var criada = await mariana.PostAsJsonAsync(url, new { instituicaoOrigemId = Seed.InstB, instituicaoDestinoId = Seed.InstA });
        Assert.Equal(HttpStatusCode.OK, criada.StatusCode);
        var aut = await criada.Json();
        Assert.True(aut.B("ativa"));
        Assert.Null(aut!["revogadaEmUtc"]);
        Assert.Equal("Mariana Souza", aut.S("pacienteNome"));
        Assert.Equal("Clínica São Bento", aut.S("instituicaoOrigemNome"));
        Assert.Equal("Clínica Santa Maria", aut.S("instituicaoDestinoNome"));

        var lista = await (await mariana.GetAsync(url)).JsonArray();
        Assert.Contains(lista, a => a.G("id") == aut.G("id") && a.B("ativa"));

        var revogada = await mariana.DeleteAsync($"/api/autorizacoes/{aut.G("id")}");
        Assert.Equal(HttpStatusCode.OK, revogada.StatusCode);
        Assert.Equal("Autorização revogada com sucesso.", await revogada.Mensagem());

        var depois = await (await mariana.GetAsync(url)).JsonArray();
        var item = Assert.Single(depois, a => a.G("id") == aut.G("id"));
        Assert.False(item.B("ativa"));
        Assert.NotNull(item!["revogadaEmUtc"]);

        var revogarDeNovo = await mariana.DeleteAsync($"/api/autorizacoes/{aut.G("id")}");
        Assert.Equal(HttpStatusCode.OK, revogarDeNovo.StatusCode);

        var inexistente = await mariana.DeleteAsync($"/api/autorizacoes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Autorizacoes_de_outro_paciente_sao_negadas()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);

        var listar = await mariana.GetAsync($"/api/pacientes/{Seed.PacienteCarlos}/autorizacoes");
        Assert.Equal(HttpStatusCode.Forbidden, listar.StatusCode);
        Assert.Equal("Acesso negado.", await listar.Mensagem());

        var conceder = await mariana.PostAsJsonAsync($"/api/pacientes/{Seed.PacienteCarlos}/autorizacoes", new { instituicaoOrigemId = Seed.InstA, instituicaoDestinoId = Seed.InstB });
        Assert.Equal(HttpStatusCode.Forbidden, conceder.StatusCode);
        Assert.Equal("Apenas o próprio paciente pode conceder autorizações de compartilhamento.", await conceder.Mensagem());

        var revogar = await mariana.DeleteAsync($"/api/autorizacoes/{Seed.AutorizacaoCarlos}");
        Assert.Equal(HttpStatusCode.Forbidden, revogar.StatusCode);
        Assert.Equal("Você só pode revogar autorizações do seu próprio cadastro.", await revogar.Mensagem());

        var equipe = await (await api.ComoAsync(Seed.EnfermeiroA)).GetAsync($"/api/pacientes/{Seed.PacienteCarlos}/autorizacoes");
        Assert.Equal(HttpStatusCode.Forbidden, equipe.StatusCode);
    }

    private static MultipartFormDataContent Multipart(string nome, string contentType, string conteudo)
        => Multipart(nome, contentType, System.Text.Encoding.UTF8.GetBytes(conteudo));

    private static MultipartFormDataContent Multipart(string nome, string contentType, byte[] bytes)
    {
        var arquivo = new ByteArrayContent(bytes);
        arquivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { arquivo, "arquivo", nome } };
    }
}
