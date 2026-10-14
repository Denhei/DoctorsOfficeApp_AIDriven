using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

[Collection(ApiCollection.Nome)]
public class CatalogoTests(ApiFixture api)
{
    [Fact]
    public async Task Health_reporta_postgres_e_mongo_online()
    {
        var resp = await api.Anonimo().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Json();
        Assert.Equal("Healthy", json.S("status"));
        Assert.Equal("Online", json["servicos"].S("postgreSql"));
        Assert.Equal("Online", json["servicos"].S("mongoDb"));
    }

    [Fact]
    public async Task Instituicoes_sao_publicas_e_listam_as_duas_clinicas_do_seed()
    {
        var resp = await api.Anonimo().GetAsync("/api/instituicoes");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var lista = await resp.JsonArray();
        Assert.Equal(2, lista.Count);
        Assert.Contains(lista, i => i.S("nome") == "Clínica Santa Maria" && i.B("ativa"));
        Assert.Contains(lista, i => i.S("nome") == "Clínica São Bento" && i.S("razaoSocial") == "São Bento Medicina Integrada Ltda");
    }

    [Fact]
    public async Task Medicos_filtram_por_instituicao()
    {
        var resp = await api.Anonimo().GetAsync($"/api/medicos?instituicaoId={Seed.InstA}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var lista = await resp.JsonArray();
        var medico = Assert.Single(lista);
        Assert.Equal(Seed.MedicoAId, medico.G("id"));
        Assert.Equal("Dr. Carlos Roberto", medico.S("nome"));
        Assert.Equal("Cardiologia", medico.S("especialidade"));
        Assert.Equal("CRM/SP 123456", medico.S("crmFicticio"));
        Assert.Equal("Clínica Santa Maria", medico.S("instituicaoNome"));

        var todos = await (await api.Anonimo().GetAsync("/api/medicos")).JsonArray();
        Assert.True(todos.Count >= 2);
    }

    [Fact]
    public async Task Slots_do_medico_incluem_passado_ocupado_e_futuros_disponiveis()
    {
        var resp = await api.Anonimo().GetAsync($"/api/slots?medicoId={Seed.MedicoAId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var slots = await resp.JsonArray();
        // Seed: 1 slot passado + 5 dias úteis x 12 slots. Outros testes podem criar slots extras.
        Assert.True(slots.Count >= 61, $"esperava >= 61 slots, obteve {slots.Count}");
        Assert.All(slots, s => Assert.Equal("Dr. Carlos Roberto", s.S("medicoNome")));
        Assert.All(slots, s => Assert.Equal(Seed.InstA, s.G("instituicaoId")));

        var passado = Assert.Single(slots, s => s.G("id") == Seed.SlotPassadoCarlos);
        Assert.False(passado.B("disponivel"));

        var ordenado = slots.Select(s => s.D("inicioUtc")).ToList();
        Assert.Equal(ordenado.OrderBy(d => d), ordenado);
    }

    [Fact]
    public async Task Slots_filtram_por_data()
    {
        var proximoDiaUtil = Enumerable.Range(1, 7)
            .Select(d => DateTime.UtcNow.Date.AddDays(d))
            .First(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        var resp = await api.Anonimo().GetAsync($"/api/slots?instituicaoId={Seed.InstB}&data={proximoDiaUtil:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var slots = await resp.JsonArray();
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(proximoDiaUtil, s.D("inicioUtc").Date));
    }

    [Fact]
    public async Task Criar_slot_exige_atendente_valida_horario_e_sobreposicao()
    {
        var atendenteA = await api.ComoAsync(Seed.AtendenteA);
        var inicio = DateTime.UtcNow.Date.AddDays(3).AddHours(22);
        var fim = inicio.AddMinutes(30);

        var anonimo = await api.Anonimo().PostAsJsonAsync("/api/slots", new { medicoId = Seed.MedicoAId, instituicaoId = Seed.InstA, inicioUtc = inicio, fimUtc = fim });
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);

        var paciente = await (await api.ComoAsync(Seed.Mariana)).PostAsJsonAsync("/api/slots", new { medicoId = Seed.MedicoAId, instituicaoId = Seed.InstA, inicioUtc = inicio, fimUtc = fim });
        Assert.Equal(HttpStatusCode.Forbidden, paciente.StatusCode);

        var fimAntesDoInicio = await atendenteA.PostAsJsonAsync("/api/slots", new { medicoId = Seed.MedicoAId, instituicaoId = Seed.InstA, inicioUtc = inicio, fimUtc = inicio });
        Assert.Equal(HttpStatusCode.BadRequest, fimAntesDoInicio.StatusCode);
        Assert.Equal("Horário de fim deve ser posterior ao início.", await fimAntesDoInicio.Mensagem());

        var medicoDeOutraInstituicao = await atendenteA.PostAsJsonAsync("/api/slots", new { medicoId = Seed.MedicoBId, instituicaoId = Seed.InstA, inicioUtc = inicio, fimUtc = fim });
        Assert.Equal(HttpStatusCode.BadRequest, medicoDeOutraInstituicao.StatusCode);
        Assert.Equal("Médico não encontrado ou não pertence a esta instituição.", await medicoDeOutraInstituicao.Mensagem());

        var criado = await atendenteA.PostAsJsonAsync("/api/slots", new { medicoId = Seed.MedicoAId, instituicaoId = Seed.InstA, inicioUtc = inicio, fimUtc = fim });
        Assert.Equal(HttpStatusCode.OK, criado.StatusCode);
        var slot = await criado.Json();
        Assert.True(slot.B("disponivel"));
        Assert.Equal(inicio, slot.D("inicioUtc"));
        Assert.Equal("Dr. Carlos Roberto", slot.S("medicoNome"));
        Assert.Equal("Clínica Santa Maria", slot.S("instituicaoNome"));

        var sobreposto = await atendenteA.PostAsJsonAsync("/api/slots", new { medicoId = Seed.MedicoAId, instituicaoId = Seed.InstA, inicioUtc = inicio.AddMinutes(15), fimUtc = fim.AddMinutes(15) });
        Assert.Equal(HttpStatusCode.BadRequest, sobreposto.StatusCode);
        Assert.Equal("Já existe um slot cadastrado para este médico no mesmo horário.", await sobreposto.Mensagem());
    }
}
