using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;
using static System.Net.HttpStatusCode;

namespace Consultorio.Api.Tests.Caracterizacao;

/// <summary>
/// Cenário ponta a ponta: chat de triagem, fila e validação do enfermeiro, agendamento,
/// atendimento médico, notificações, pesquisa de satisfação e dashboard.
/// Um único teste porque cada passo depende do estado deixado pelo anterior.
/// </summary>
[Collection(ApiCollection.Nome)]
public class TriagemAgendamentoTests(ApiFixture api)
{
    private const string ForaDoRoteiro = "Nesta demonstração, consigo coletar informações para a equipe. Para outras dúvidas, solicite atendimento humano.";

    [Fact]
    public async Task Fluxo_completo_da_triagem_ao_dashboard()
    {
        var mariana = await api.ComoAsync(Seed.Mariana);
        var carlos = await api.ComoAsync(Seed.Carlos);
        var enfA = await api.ComoAsync(Seed.EnfermeiroA);
        var enfB = await api.ComoAsync(Seed.EnfermeiroB);
        var medA = await api.ComoAsync(Seed.MedicoA);
        var medB = await api.ComoAsync(Seed.MedicoB);
        var atdA = await api.ComoAsync(Seed.AtendenteA);
        var atdB = await api.ComoAsync(Seed.AtendenteB);
        var admin = await api.ComoAsync(Seed.Admin);

        // 1. Início da coleta
        Assert.Equal(Forbidden, (await atdA.PostAsJsonAsync("/api/coletas", new { instituicaoId = Seed.InstA })).StatusCode);

        var inicio = await mariana.PostAsJsonAsync("/api/coletas", new { instituicaoId = Seed.InstA });
        Assert.Equal(OK, inicio.StatusCode);
        var conversa = await inicio.Json();
        var coletaId = conversa.G("coletaId");
        Assert.Equal("EmColeta", conversa.S("statusColeta"));
        Assert.Equal(1, conversa.I("etapaAtual"));
        Assert.Equal("Mariana Souza", conversa.S("pacienteNome"));
        Assert.Equal("Clínica Santa Maria", conversa.S("instituicaoNome"));
        Assert.Null(conversa["resumoValidado"]);
        Assert.Equal("não informado", conversa["resumoPreliminar"].S("queixa"));
        var boasVindas = Assert.Single(Mensagens(conversa));
        Assert.Equal("Bot", boasVindas.S("remetente"));
        Assert.Equal("Opcoes", boasVindas.S("tipo"));
        Assert.StartsWith("Olá, Mariana Souza! Sou o assistente virtual", boasVindas.S("conteudo"));
        Assert.Contains("Nascimento: 14/08/1992", boasVindas.S("conteudo"));
        Assert.Equal(new[] { "Sim, confirmo meus dados", "Preciso corrigir meu cadastro" }, Opcoes(boasVindas));

        // Leitura: paciente só vê a própria coleta; equipe vê qualquer uma
        var outroPaciente = await carlos.GetAsync($"/api/coletas/{coletaId}");
        Assert.Equal(Forbidden, outroPaciente.StatusCode);
        Assert.Equal("Acesso negado aos dados de outro paciente.", await outroPaciente.Mensagem());
        Assert.Equal(OK, (await mariana.GetAsync($"/api/coletas/{coletaId}")).StatusCode);
        Assert.Equal(OK, (await enfA.GetAsync($"/api/coletas/{coletaId}")).StatusCode);
        Assert.Equal(NotFound, (await mariana.GetAsync($"/api/coletas/{Guid.NewGuid()}")).StatusCode);

        // 2. Roteiro determinístico (ramo "Sim" em alergia, "Não sei" em medicamentos)
        conversa = await Responder(mariana, coletaId, "Sim, confirmo meus dados", "m1");
        Assert.Equal(2, conversa.I("etapaAtual"));
        Assert.Equal(3, Mensagens(conversa).Count);
        Assert.Equal("Paciente", Mensagens(conversa)[1].S("remetente"));
        Assert.Equal("Texto", Mensagens(conversa)[1].S("tipo"));
        Assert.Contains("queixa principal", Ultima(conversa).S("conteudo"));
        Assert.Contains("Clínica Santa Maria", Ultima(conversa).S("conteudo"));

        var repetida = await Responder(mariana, coletaId, "Sim, confirmo meus dados", "m1");
        Assert.Equal(2, repetida.I("etapaAtual"));
        Assert.Equal(3, Mensagens(repetida).Count);

        conversa = await Responder(mariana, coletaId, "  Dor de cabeça forte  ", "m2");
        Assert.Equal(3, conversa.I("etapaAtual"));
        Assert.Equal("Dor de cabeça forte", conversa["resumoPreliminar"].S("queixa"));
        Assert.Contains("Há quanto tempo", Ultima(conversa).S("conteudo"));

        conversa = await Responder(mariana, coletaId, "2 dias", "m3");
        Assert.Equal(4, conversa.I("etapaAtual"));
        Assert.Equal("2 dias", conversa["resumoPreliminar"].S("duracao"));
        Assert.Equal("Opcoes", Ultima(conversa).S("tipo"));
        Assert.Equal(new[] { "Não possuo alergias", "Sim, possuo alergia", "Não sei informar" }, Opcoes(Ultima(conversa)));

        conversa = await Responder(mariana, coletaId, "Sim, possuo alergia", "m4");
        Assert.Equal(5, conversa.I("etapaAtual"));
        Assert.Contains("quais substâncias", Ultima(conversa).S("conteudo"));

        conversa = await Responder(mariana, coletaId, "Dipirona", "m5");
        Assert.Equal(6, conversa.I("etapaAtual"));
        Assert.Equal("Dipirona", conversa["resumoPreliminar"].S("alergias"));
        Assert.Contains("uso de algum medicamento", Ultima(conversa).S("conteudo"));
        Assert.Equal(new[] { "Não tomo medicamentos", "Sim, tomo medicamentos", "Não sei informar" }, Opcoes(Ultima(conversa)));

        conversa = await Responder(mariana, coletaId, "Não sei informar", "m6");
        Assert.Equal(8, conversa.I("etapaAtual"));
        Assert.Equal("Não sabe informar", conversa["resumoPreliminar"].S("medicamentos"));
        Assert.Contains("informação ou observação", Ultima(conversa).S("conteudo"));
        Assert.Equal(new[] { "Não tenho observações adicionais" }, Opcoes(Ultima(conversa)));

        conversa = await Responder(mariana, coletaId, "Não tenho observações adicionais", "m7");
        Assert.Equal(9, conversa.I("etapaAtual"));
        Assert.Equal("Nenhuma observação adicional", conversa["resumoPreliminar"].S("observacoes"));
        var resumo = Ultima(conversa).S("conteudo");
        Assert.Contains("Resumo da Coleta Preliminar", resumo);
        Assert.Contains("Queixa relatada: Dor de cabeça forte. Duração relatada: 2 dias. Alergias relatadas: Dipirona. Medicamentos relatados: Não sabe informar. Observações: Nenhuma observação adicional.", resumo);
        Assert.Equal(new[] { "Confirmar e Enviar para o Enfermeiro" }, Opcoes(Ultima(conversa)));

        conversa = await Responder(mariana, coletaId, "mais uma coisa", "m8");
        Assert.Equal(9, conversa.I("etapaAtual"));
        Assert.Equal(ForaDoRoteiro, Ultima(conversa).S("conteudo"));

        // 3. Agendar antes da validação é recusado
        var slotA = await PrimeiroSlotDisponivel(Seed.MedicoAId);
        var cedo = await Agendar(mariana, slotA.G("id"), coletaId);
        Assert.Equal(BadRequest, cedo.StatusCode);
        Assert.Equal("O agendamento só é permitido após a validação da triagem pelo enfermeiro.", await cedo.Mensagem());

        // 4. Finalização
        var finalizada = await mariana.PostAsync($"/api/coletas/{coletaId}/finalizacao", null);
        Assert.Equal(OK, finalizada.StatusCode);
        conversa = await finalizada.Json();
        Assert.Equal("PendenteValidacao", conversa.S("statusColeta"));
        Assert.Contains("enviada com sucesso", Ultima(conversa).S("conteudo"));

        var depois = await mariana.PostAsJsonAsync($"/api/coletas/{coletaId}/mensagens", new { conteudo = "x", clienteMessageId = "m9" });
        Assert.Equal(BadRequest, depois.StatusCode);
        Assert.Equal("Esta coleta já foi enviada para triagem e não pode receber novas respostas.", await depois.Mensagem());

        // 5. Fila do enfermeiro é por instituição
        var filaA = await (await enfA.GetAsync("/api/coletas")).JsonArray();
        var item = Assert.Single(filaA, c => c.G("id") == coletaId);
        Assert.Equal("Mariana Souza", item.S("pacienteNome"));
        Assert.Equal("Clínica Santa Maria", item.S("instituicaoNome"));
        Assert.Equal("PendenteValidacao", item.S("status"));
        Assert.Contains("Alergias relatadas: Dipirona.", item.S("resumoPreliminarTexto"));

        Assert.DoesNotContain(await (await enfB.GetAsync("/api/coletas")).JsonArray(), c => c.G("id") == coletaId);
        Assert.Equal(Forbidden, (await mariana.GetAsync("/api/coletas")).StatusCode);
        var filaAdmin = await admin.GetAsync("/api/coletas");
        Assert.Equal(OK, filaAdmin.StatusCode);
        Assert.Empty(await filaAdmin.JsonArray());

        // 6. Validação pelo enfermeiro
        const string resumoValidado = "Cefaleia intensa há 2 dias; alergia a dipirona.";
        var validacao = new { aprovadoParaAgendamento = true, resumoCorrigido = resumoValidado };
        Assert.Equal(Forbidden, (await mariana.PutAsJsonAsync($"/api/coletas/{coletaId}/validacao", validacao)).StatusCode);

        var validada = await enfA.PutAsJsonAsync($"/api/coletas/{coletaId}/validacao", validacao);
        Assert.Equal(OK, validada.StatusCode);
        Assert.Equal("Triagem validada com sucesso!", await validada.Mensagem());

        var deNovo = await enfA.PutAsJsonAsync($"/api/coletas/{coletaId}/validacao", validacao);
        Assert.Equal(BadRequest, deNovo.StatusCode);
        Assert.Equal("Coleta não encontrada ou já validada.", await deNovo.Mensagem());

        conversa = await (await mariana.GetAsync($"/api/coletas/{coletaId}")).Json();
        Assert.Equal("Validada", conversa.S("statusColeta"));
        Assert.Equal(resumoValidado, conversa.S("resumoValidado"));
        Assert.Contains("Validação pela Enfermagem Concluída", Ultima(conversa).S("conteudo"));
        Assert.Contains(resumoValidado, Ultima(conversa).S("conteudo"));

        // 7. Agendamento
        var slotB = await PrimeiroSlotDisponivel(Seed.MedicoBId);
        var instituicaoErrada = await Agendar(mariana, slotB.G("id"), coletaId);
        Assert.Equal(BadRequest, instituicaoErrada.StatusCode);
        Assert.Equal("A instituição do médico escolhido deve ser a mesma da coleta validada.", await instituicaoErrada.Mensagem());

        var paraOutro = await Agendar(carlos, slotA.G("id"), coletaId);
        Assert.Equal(Forbidden, paraOutro.StatusCode);
        Assert.Equal("Você só pode agendar consultas para seu próprio cadastro.", await paraOutro.Mensagem());

        var passado = await Agendar(mariana, Seed.SlotPassadoCarlos, coletaId);
        Assert.Equal(BadRequest, passado.StatusCode);
        Assert.Equal("Não é possível agendar consultas em horários passados.", await passado.Mensagem());

        var coletaInexistente = await Agendar(mariana, slotA.G("id"), Guid.NewGuid());
        Assert.Equal(BadRequest, coletaInexistente.StatusCode);
        Assert.Equal("Coleta inicial não encontrada.", await coletaInexistente.Mensagem());

        var agendada = await Agendar(mariana, slotA.G("id"), coletaId);
        Assert.Equal(OK, agendada.StatusCode);
        var consulta = await agendada.Json();
        var consultaId = consulta.G("id");
        Assert.Equal("Agendada", consulta.S("status"));
        Assert.Equal(Seed.PacienteMariana, consulta.G("pacienteId"));
        Assert.Equal("Mariana Souza", consulta.S("pacienteNome"));
        Assert.Equal(Seed.MedicoAId, consulta.G("medicoId"));
        Assert.Equal("Dr. Carlos Roberto", consulta.S("medicoNome"));
        Assert.Equal("Cardiologia", consulta.S("especialidade"));
        Assert.Equal(Seed.InstA, consulta.G("instituicaoId"));
        Assert.Equal(slotA.G("id"), consulta.G("slotId"));
        Assert.Equal(slotA.D("inicioUtc"), consulta.D("inicioUtc"));
        Assert.Equal(coletaId, consulta.G("coletaId"));
        Assert.Equal(resumoValidado, consulta.S("resumoValidado"));
        Assert.False(consulta.B("temEvolucao"));
        Assert.False(consulta.B("temPesquisaRespondida"));

        var mesmoSlot = await Agendar(mariana, slotA.G("id"), coletaId);
        Assert.Equal(Conflict, mesmoSlot.StatusCode);
        Assert.Equal("Este horário já foi reservado por outro paciente (Conflito de Slot).", await mesmoSlot.Mensagem());

        var slotAAgora = Assert.Single(await Slots(Seed.MedicoAId), s => s.G("id") == slotA.G("id"));
        Assert.False(slotAAgora.B("disponivel"));

        // 8. Segunda triagem (ramos "Não sei" e "Sim, tomo") na outra clínica e conflito de horário do próprio paciente
        var (coletaB, conversaB) = await TriagemCompleta(mariana, Seed.InstB);
        Assert.Equal("Não sabe informar", conversaB["resumoPreliminar"].S("alergias"));
        Assert.Equal("Losartana 50mg", conversaB["resumoPreliminar"].S("medicamentos"));
        Assert.Equal("Observação livre", conversaB["resumoPreliminar"].S("observacoes"));

        Assert.Equal(OK, (await enfB.PutAsJsonAsync($"/api/coletas/{coletaB}/validacao", new { aprovadoParaAgendamento = true, resumoCorrigido = "ok" })).StatusCode);

        var slotBMesmoHorario = Assert.Single(await Slots(Seed.MedicoBId), s => s.D("inicioUtc") == slotA.D("inicioUtc"));
        var sobreposta = await Agendar(mariana, slotBMesmoHorario.G("id"), coletaB);
        Assert.Equal(Conflict, sobreposta.StatusCode);
        Assert.Equal("O paciente já possui uma consulta agendada neste mesmo intervalo de horário.", await sobreposta.Mensagem());

        var slotBLivre = (await Slots(Seed.MedicoBId)).First(s => s.B("disponivel") && s.D("inicioUtc") > slotA.D("inicioUtc").AddHours(2));
        var agendadaB = await Agendar(mariana, slotBLivre.G("id"), coletaB);
        Assert.Equal(OK, agendadaB.StatusCode);
        var consultaBId = (await agendadaB.Json()).G("id");

        // 9. Visibilidade das consultas por perfil
        Assert.True(await Lista(mariana, consultaId));
        Assert.True(await Lista(mariana, consultaBId));
        Assert.True(await Lista(medA, consultaId));
        Assert.False(await Lista(medA, consultaBId));
        Assert.False(await Lista(medB, consultaId));
        Assert.True(await Lista(medB, consultaBId));
        Assert.True(await Lista(atdA, consultaId));
        Assert.False(await Lista(atdB, consultaId));
        Assert.True(await Lista(enfB, consultaBId));
        Assert.True(await Lista(admin, consultaId));
        Assert.True(await Lista(admin, consultaBId));
        Assert.False(await Lista(carlos, consultaId));
        Assert.False(await Lista(admin, consultaBId, $"?instituicaoId={Seed.InstA}"));
        Assert.True(await Lista(atdA, consultaId, $"?pacienteId={Seed.PacienteMariana}"));
        Assert.False(await Lista(atdA, consultaId, $"?pacienteId={Seed.PacienteCarlos}"));

        var porIdOutroPaciente = await carlos.GetAsync($"/api/consultas/{consultaId}");
        Assert.Equal(Forbidden, porIdOutroPaciente.StatusCode);
        Assert.Equal("Acesso negado.", await porIdOutroPaciente.Mensagem());
        Assert.Equal(OK, (await medB.GetAsync($"/api/consultas/{consultaId}")).StatusCode);
        Assert.Equal(OK, (await atdB.GetAsync($"/api/consultas/{consultaId}")).StatusCode);
        Assert.Equal(NotFound, (await mariana.GetAsync($"/api/consultas/{Guid.NewGuid()}")).StatusCode);

        // 10. Transições de status
        var iniciarComoPaciente = await Status(mariana, consultaId, "EmAtendimento");
        Assert.Equal(Forbidden, iniciarComoPaciente.StatusCode);
        Assert.Equal("Apenas o médico responsável pode iniciar o atendimento.", await iniciarComoPaciente.Mensagem());
        Assert.Equal(Forbidden, (await Status(medB, consultaId, "EmAtendimento")).StatusCode);

        var emAtendimento = await Status(medA, consultaId, "EmAtendimento");
        Assert.Equal(OK, emAtendimento.StatusCode);
        Assert.Equal("Status atualizado com sucesso.", await emAtendimento.Mensagem());
        Assert.Equal("EmAtendimento", (await (await medA.GetAsync($"/api/consultas/{consultaId}")).Json()).S("status"));

        var transicaoInvalida = await Status(medA, consultaId, "Agendada");
        Assert.Equal(BadRequest, transicaoInvalida.StatusCode);
        Assert.Equal("application/problem+json", transicaoInvalida.Content.Headers.ContentType?.MediaType);

        var cancelarDeOutro = await Status(carlos, consultaBId, "Cancelada");
        Assert.Equal(Forbidden, cancelarDeOutro.StatusCode);
        Assert.Equal("Acesso negado.", await cancelarDeOutro.Mensagem());

        Assert.Equal(OK, (await Status(mariana, consultaBId, "Cancelada")).StatusCode);
        Assert.Equal("Cancelada", (await (await mariana.GetAsync($"/api/consultas/{consultaBId}")).Json()).S("status"));
        Assert.True(Assert.Single(await Slots(Seed.MedicoBId), s => s.G("id") == slotBLivre.G("id")).B("disponivel"));
        Assert.Equal(BadRequest, (await Status(mariana, Guid.NewGuid(), "Cancelada")).StatusCode);

        // 11. Evolução e conclusão
        var evolucaoOutroMedico = await medB.PutAsJsonAsync($"/api/consultas/{consultaId}/evolucao", new { texto = "x" });
        Assert.Equal(Forbidden, evolucaoOutroMedico.StatusCode);
        Assert.Equal("Apenas o médico responsável por esta consulta pode registrar a evolução.", await evolucaoOutroMedico.Mensagem());
        Assert.Equal(Forbidden, (await mariana.PutAsJsonAsync($"/api/consultas/{consultaId}/evolucao", new { texto = "x" })).StatusCode);

        var evolucaoVazia = await medA.PutAsJsonAsync($"/api/consultas/{consultaId}/evolucao", new { texto = "  " });
        Assert.Equal(BadRequest, evolucaoVazia.StatusCode);
        Assert.Equal("O texto da evolução clínica é obrigatório.", await evolucaoVazia.Mensagem());

        var concluirSemEvolucao = await medA.PostAsync($"/api/consultas/{consultaId}/conclusao", null);
        Assert.Equal(BadRequest, concluirSemEvolucao.StatusCode);
        Assert.Equal("É obrigatório registrar a evolução médica antes de concluir o atendimento.", await concluirSemEvolucao.Mensagem());

        var evolucao = await medA.PutAsJsonAsync($"/api/consultas/{consultaId}/evolucao", new { texto = "Cefaleia tensional. Orientado repouso." });
        Assert.Equal(OK, evolucao.StatusCode);
        var ev = await evolucao.Json();
        Assert.Equal(consultaId, ev.G("consultaId"));
        Assert.Equal(Seed.MedicoAId, ev.G("medicoId"));
        Assert.Equal("Dr. Carlos Roberto", ev.S("medicoNome"));
        Assert.Equal("Cefaleia tensional. Orientado repouso.", ev.S("texto"));

        var evolucaoAtualizada = await (await medA.PutAsJsonAsync($"/api/consultas/{consultaId}/evolucao", new { texto = "Cefaleia tensional. Prescrito analgésico." })).Json();
        Assert.Equal(ev.G("id"), evolucaoAtualizada.G("id"));
        Assert.Equal("Cefaleia tensional. Prescrito analgésico.", evolucaoAtualizada.S("texto"));

        var concluirOutroMedico = await medB.PostAsync($"/api/consultas/{consultaId}/conclusao", null);
        Assert.Equal(Forbidden, concluirOutroMedico.StatusCode);
        Assert.Equal("Apenas o médico responsável pode concluir o atendimento.", await concluirOutroMedico.Mensagem());

        var concluida = await medA.PostAsync($"/api/consultas/{consultaId}/conclusao", null);
        Assert.Equal(OK, concluida.StatusCode);
        var concluidaJson = await concluida.Json();
        Assert.Equal("Concluida", concluidaJson.S("status"));
        Assert.True(concluidaJson.B("temEvolucao"));
        Assert.False(concluidaJson.B("temPesquisaRespondida"));
        Assert.Equal("Dr. Carlos Roberto", concluidaJson.S("medicoNome"));

        var concluidaDeNovo = await medA.PostAsync($"/api/consultas/{consultaId}/conclusao", null);
        Assert.Equal(OK, concluidaDeNovo.StatusCode);
        Assert.Equal("Concluida", (await concluidaDeNovo.Json()).S("status"));

        var cancelarConcluida = await Status(mariana, consultaId, "Cancelada");
        Assert.Equal(BadRequest, cancelarConcluida.StatusCode);
        Assert.Equal("Consultas já concluídas não podem ser canceladas.", await cancelarConcluida.Mensagem());

        // 12. Notificações simuladas geradas uma única vez na conclusão
        var notificacoes = (await (await mariana.GetAsync("/api/notificacoes")).JsonArray())
            .Where(n => n!["consultaId"] is not null && n.G("consultaId") == consultaId).ToList();
        Assert.Equal(2, notificacoes.Count);
        Assert.All(notificacoes, n => Assert.Equal("ConvitePesquisa", n.S("finalidade")));
        Assert.All(notificacoes, n => Assert.Null(n!["lidaEmUtc"]));
        var whatsapp = Assert.Single(notificacoes, n => n.S("canal") == "WhatsApp");
        Assert.Equal("Pesquisa de Satisfação - Clínica Santa Maria", whatsapp.S("titulo"));
        Assert.Contains("Dr. Carlos Roberto", whatsapp.S("texto"));
        var email = Assert.Single(notificacoes, n => n.S("canal") == "Email");
        Assert.Equal("Como foi seu atendimento?", email.S("titulo"));
        Assert.Contains("Prezado(a) Mariana Souza", email.S("texto"));
        Assert.Equal(Forbidden, (await enfA.GetAsync("/api/notificacoes")).StatusCode);

        // 13. Pesquisa de satisfação
        Assert.Equal(NotFound, (await mariana.GetAsync($"/api/consultas/{consultaId}/pesquisa")).StatusCode);

        var naoConcluida = await mariana.PutAsJsonAsync($"/api/consultas/{consultaBId}/pesquisa", new { nota = 5, percepcao = "Melhor" });
        Assert.Equal(BadRequest, naoConcluida.StatusCode);
        Assert.Equal("A pesquisa de satisfação só pode ser respondida após a conclusão da consulta.", await naoConcluida.Mensagem());

        var notaInvalida = await mariana.PutAsJsonAsync($"/api/consultas/{consultaId}/pesquisa", new { nota = 6, percepcao = "Melhor" });
        Assert.Equal(BadRequest, notaInvalida.StatusCode);
        Assert.Equal("A nota de avaliação deve ser entre 1 e 5.", await notaInvalida.Mensagem());

        var consultaDeOutro = await carlos.PutAsJsonAsync($"/api/consultas/{consultaId}/pesquisa", new { nota = 5, percepcao = "Melhor" });
        Assert.Equal(BadRequest, consultaDeOutro.StatusCode);
        Assert.Equal("Consulta não encontrada.", await consultaDeOutro.Mensagem());

        var respondida = await mariana.PutAsJsonAsync($"/api/consultas/{consultaId}/pesquisa", new { nota = 4, percepcao = "Igual", comentario = "Bom atendimento" });
        Assert.Equal(OK, respondida.StatusCode);
        var pesquisa = await respondida.Json();
        Assert.Equal(4, pesquisa.I("nota"));
        Assert.Equal("Igual", pesquisa.S("percepcao"));
        Assert.Equal("Bom atendimento", pesquisa.S("comentario"));
        Assert.Equal(consultaId, pesquisa.G("consultaId"));
        Assert.Equal(Seed.PacienteMariana, pesquisa.G("pacienteId"));

        var respondidaDeNovo = await mariana.PutAsJsonAsync($"/api/consultas/{consultaId}/pesquisa", new { nota = 1, percepcao = "Pior" });
        Assert.Equal(BadRequest, respondidaDeNovo.StatusCode);
        Assert.Equal("Esta pesquisa de satisfação já foi respondida previamente.", await respondidaDeNovo.Mensagem());

        Assert.Equal(4, (await (await mariana.GetAsync($"/api/consultas/{consultaId}/pesquisa")).Json()).I("nota"));
        Assert.True((await (await mariana.GetAsync($"/api/consultas/{consultaId}")).Json()).B("temPesquisaRespondida"));

        // 14. Dashboard por instituição e global
        Assert.Equal(Forbidden, (await mariana.GetAsync("/api/dashboard")).StatusCode);

        var dashA = await (await atdA.GetAsync("/api/dashboard")).Json();
        Assert.Equal(0, dashA.I("totalConsultasAgendadas"));
        Assert.Equal(0, dashA.I("totalConsultasEmAtendimento"));
        Assert.Equal(2, dashA.I("totalConsultasConcluidas"));
        Assert.Equal(0, dashA.I("totalConsultasCanceladas"));
        Assert.Equal(0, dashA.I("totalColetasPendentesValidacao"));
        Assert.Equal(2, dashA.I("totalPesquisasRespondidas"));
        Assert.Equal(4.5, dashA!["mediaSatisfacao"]!.GetValue<double>());

        var dashB = await (await atdB.GetAsync("/api/dashboard")).Json();
        Assert.Equal(0, dashB.I("totalConsultasConcluidas"));
        Assert.Equal(1, dashB.I("totalConsultasCanceladas"));
        Assert.Equal(0, dashB.I("totalPesquisasRespondidas"));
        Assert.Equal(0, dashB!["mediaSatisfacao"]!.GetValue<double>());

        var dashGlobal = await (await admin.GetAsync("/api/dashboard")).Json();
        Assert.Equal(2, dashGlobal.I("totalConsultasConcluidas"));
        Assert.Equal(1, dashGlobal.I("totalConsultasCanceladas"));
        Assert.Equal(2, dashGlobal.I("totalPesquisasRespondidas"));
        Assert.Equal(4.5, dashGlobal!["mediaSatisfacao"]!.GetValue<double>());
    }

    [Fact]
    public async Task Roteiro_com_respostas_negativas_pula_as_etapas_de_detalhe()
    {
        var carlos = await api.ComoAsync(Seed.Carlos);
        var conversa = await (await carlos.PostAsJsonAsync("/api/coletas", new { instituicaoId = Seed.InstA })).Json();
        var coletaId = conversa.G("coletaId");
        Assert.Contains("Olá, Carlos Silva!", Ultima(conversa).S("conteudo"));

        conversa = await Responder(carlos, coletaId, "Sim, confirmo meus dados", "n1");
        conversa = await Responder(carlos, coletaId, "Tosse seca", "n2");
        conversa = await Responder(carlos, coletaId, "3 dias", "n3");
        Assert.Equal(4, conversa.I("etapaAtual"));

        conversa = await Responder(carlos, coletaId, "Não possuo alergias", "n4");
        Assert.Equal(6, conversa.I("etapaAtual"));
        Assert.Equal("Nenhuma relatada", conversa["resumoPreliminar"].S("alergias"));

        conversa = await Responder(carlos, coletaId, "Não tomo medicamentos", "n5");
        Assert.Equal(8, conversa.I("etapaAtual"));
        Assert.Equal("Nenhum relatado", conversa["resumoPreliminar"].S("medicamentos"));

        conversa = await Responder(carlos, coletaId, "Sinto tontura ao levantar", "n6");
        Assert.Equal(9, conversa.I("etapaAtual"));
        Assert.Equal("Sinto tontura ao levantar", conversa["resumoPreliminar"].S("observacoes"));
        Assert.Contains("Observações: Sinto tontura ao levantar.", Ultima(conversa).S("conteudo"));
        Assert.Equal("EmColeta", conversa.S("statusColeta"));

        // Resposta vazia em etapa de texto vira "não informado"
        var outra = await (await carlos.PostAsJsonAsync("/api/coletas", new { instituicaoId = Seed.InstA })).Json();
        var outraId = outra.G("coletaId");
        await Responder(carlos, outraId, "Sim, confirmo meus dados", "v1");
        outra = await Responder(carlos, outraId, "   ", "v2");
        Assert.Equal(3, outra.I("etapaAtual"));
        Assert.Equal("não informado", outra["resumoPreliminar"].S("queixa"));
        Assert.Equal("", Mensagens(outra)[^2].S("conteudo"));
    }

    private static JsonArray Mensagens(JsonNode conversa) => conversa["mensagens"]!.AsArray();

    private static JsonNode Ultima(JsonNode conversa) => Mensagens(conversa)[^1]!;

    private static IEnumerable<string> Opcoes(JsonNode? mensagem) => mensagem!["opcoes"]!.AsArray().Select(o => o!.GetValue<string>());

    private static async Task<JsonNode> Responder(HttpClient paciente, Guid coletaId, string conteudo, string clienteMessageId)
    {
        var resp = await paciente.PostAsJsonAsync($"/api/coletas/{coletaId}/mensagens", new { conteudo, clienteMessageId });
        Assert.Equal(OK, resp.StatusCode);
        return await resp.Json();
    }

    private async Task<(Guid coletaId, JsonNode conversa)> TriagemCompleta(HttpClient paciente, Guid instituicaoId)
    {
        var conversa = await (await paciente.PostAsJsonAsync("/api/coletas", new { instituicaoId })).Json();
        var coletaId = conversa.G("coletaId");
        var prefixo = Guid.NewGuid().ToString("N");
        var respostas = new[] { "Sim, confirmo meus dados", "Dor lombar", "1 semana", "Não sei informar", "Sim, tomo medicamentos", "Losartana 50mg", "Observação livre" };
        for (var i = 0; i < respostas.Length; i++)
            conversa = await Responder(paciente, coletaId, respostas[i], $"{prefixo}-{i}");
        Assert.Equal(9, conversa.I("etapaAtual"));
        var finalizada = await paciente.PostAsync($"/api/coletas/{coletaId}/finalizacao", null);
        Assert.Equal(OK, finalizada.StatusCode);
        return (coletaId, await finalizada.Json());
    }

    private async Task<JsonArray> Slots(Guid medicoId) => await (await api.Anonimo().GetAsync($"/api/slots?medicoId={medicoId}")).JsonArray();

    private async Task<JsonNode> PrimeiroSlotDisponivel(Guid medicoId)
        => (await Slots(medicoId)).First(s => s.B("disponivel") && s.D("inicioUtc") > DateTime.UtcNow.AddHours(1))!;

    private static Task<HttpResponseMessage> Agendar(HttpClient cliente, Guid slotId, Guid coletaId)
        => cliente.PostAsJsonAsync("/api/consultas", new { pacienteId = Seed.PacienteMariana, slotId, coletaId });

    private static Task<HttpResponseMessage> Status(HttpClient cliente, Guid consultaId, string novoStatus)
        => cliente.PatchAsJsonAsync($"/api/consultas/{consultaId}/status", new { novoStatus });

    private static async Task<bool> Lista(HttpClient cliente, Guid consultaId, string query = "")
    {
        var resp = await cliente.GetAsync("/api/consultas" + query);
        Assert.Equal(OK, resp.StatusCode);
        return (await resp.JsonArray()).Any(c => c.G("id") == consultaId);
    }
}
