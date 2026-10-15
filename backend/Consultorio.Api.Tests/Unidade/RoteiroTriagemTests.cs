using Consultorio.Api.Models.Mongo;
using Consultorio.Api.Services;
using Xunit;

namespace Consultorio.Api.Tests.Unidade;

/// <summary>Roteiro puro: testável sem banco, sem Mongo e sem HTTP.</summary>
public class RoteiroTriagemTests
{
    [Theory]
    [InlineData(1, "Sim, confirmo meus dados", 2)]
    [InlineData(2, "Dor de cabeça", 3)]
    [InlineData(3, "2 dias", 4)]
    [InlineData(4, "Sim, possuo alergia", 5)]
    [InlineData(4, "Não possuo alergias", 6)]
    [InlineData(4, "Não sei informar", 6)]
    [InlineData(5, "Dipirona", 6)]
    [InlineData(6, "Sim, tomo medicamentos", 7)]
    [InlineData(6, "Não tomo medicamentos", 8)]
    [InlineData(7, "Losartana", 8)]
    [InlineData(8, "Não tenho observações adicionais", 9)]
    public void Avancar_segue_as_transicoes_do_roteiro(int etapa, string resposta, int esperada)
        => Assert.Equal(esperada, RoteiroTriagem.Avancar(new ResumoPreliminarMongo(), etapa, resposta));

    [Fact]
    public void Avancar_depois_do_resumo_nao_muda_etapa_nem_resumo()
    {
        var resumo = new ResumoPreliminarMongo { Queixa = "tosse" };

        Assert.Null(RoteiroTriagem.Avancar(resumo, 9, "mais uma coisa"));
        Assert.Equal("tosse", resumo.Queixa);
    }

    [Fact]
    public void Avancar_preenche_o_resumo_com_as_respostas_e_os_rotulos_dos_ramos_negativos()
    {
        var resumo = new ResumoPreliminarMongo();

        RoteiroTriagem.Avancar(resumo, 2, "  ");
        RoteiroTriagem.Avancar(resumo, 3, "1 semana");
        RoteiroTriagem.Avancar(resumo, 4, "Não sei informar");
        RoteiroTriagem.Avancar(resumo, 6, "Não tomo medicamentos");
        RoteiroTriagem.Avancar(resumo, 8, "Não tenho observações adicionais");

        Assert.Equal("não informado", resumo.Queixa);
        Assert.Equal("1 semana", resumo.Duracao);
        Assert.Equal("Não sabe informar", resumo.Alergias);
        Assert.Equal("Nenhum relatado", resumo.Medicamentos);
        Assert.Equal("Nenhuma observação adicional", resumo.Observacoes);
    }

    [Fact]
    public void Pergunta_existe_uma_vez_por_etapa_e_tem_opcoes_so_nas_etapas_de_escolha()
    {
        var comOpcoes = new[] { 4, 6, 8, 9 };

        for (var etapa = 2; etapa <= 9; etapa++)
        {
            var pergunta = RoteiroTriagem.Pergunta(etapa, "Clínica X", new ResumoPreliminarMongo());

            Assert.Equal("Bot", pergunta.Remetente);
            Assert.Equal(comOpcoes.Contains(etapa) ? "Opcoes" : "Texto", pergunta.Tipo);
            Assert.Equal(comOpcoes.Contains(etapa), pergunta.Opcoes is { Count: > 0 });
        }

        Assert.Contains("Clínica X", RoteiroTriagem.Pergunta(2, "Clínica X", new()).Conteudo);
        Assert.Throws<ArgumentOutOfRangeException>(() => RoteiroTriagem.Pergunta(1, "x", new()));
    }
}
