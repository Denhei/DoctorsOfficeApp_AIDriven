using System.Net;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Consultorio.Api.Tests.Caracterizacao;

[Collection(ApiCollection.Nome)]
public class CompatibilidadeMongoTests(ApiFixture api)
{
    [Fact]
    public async Task Conversa_gravada_no_formato_antigo_continua_legivel()
    {
        // Documentos criados antes da refatoração têm os campos Status e VersaoRoteiro, que o modelo
        // atual não declara. Sem BsonIgnoreExtraElements a desserialização falharia.
        var carlos = await api.ComoAsync(Seed.Carlos);
        var conversa = await (await carlos.PostAsJsonAsync("/api/coletas", new { instituicaoId = Seed.InstA })).Json();
        var coletaId = conversa.G("coletaId");

        var conversas = new MongoClient(api.MongoConnectionString)
            .GetDatabase(ApiFixture.MongoDatabaseName)
            .GetCollection<BsonDocument>("conversas");
        var filtro = Builders<BsonDocument>.Filter.Eq("_id", conversa.S("conversaId"));
        var atualizacao = Builders<BsonDocument>.Update.Set("Status", "EmAndamento").Set("VersaoRoteiro", 1);
        var resultado = await conversas.UpdateOneAsync(filtro, atualizacao);
        Assert.Equal(1, resultado.ModifiedCount);

        var lida = await carlos.GetAsync($"/api/coletas/{coletaId}");
        Assert.Equal(HttpStatusCode.OK, lida.StatusCode);
        Assert.Equal(1, (await lida.Json()).I("etapaAtual"));

        var respondida = await carlos.PostAsJsonAsync($"/api/coletas/{coletaId}/mensagens", new { conteudo = "Sim, confirmo meus dados", clienteMessageId = "antigo-1" });
        Assert.Equal(HttpStatusCode.OK, respondida.StatusCode);
        Assert.Equal(2, (await respondida.Json()).I("etapaAtual"));
    }
}
