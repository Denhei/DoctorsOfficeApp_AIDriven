using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Consultorio.Api.Models.Mongo;

[BsonIgnoreExtraElements]
public class ConversaMongo
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonRepresentation(BsonType.String)]
    public string ColetaId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public string PacienteId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public string InstituicaoId { get; set; } = string.Empty;

    public int EtapaAtual { get; set; } = 1;

    public ResumoPreliminarMongo ResumoPreliminar { get; set; } = new();

    public List<MensagemMongo> Mensagens { get; set; } = new();

    public DateTime CriadaEmUtc { get; set; } = DateTime.UtcNow;

    public DateTime AtualizadaEmUtc { get; set; } = DateTime.UtcNow;
}

public class ResumoPreliminarMongo
{
    public string Queixa { get; set; } = "não informado";
    public string Duracao { get; set; } = "não informado";
    public string Alergias { get; set; } = "não informado";
    public string Medicamentos { get; set; } = "não informado";
    public string Observacoes { get; set; } = "não informado";

    public string GerarTextoFormatado()
    {
        return $"Queixa relatada: {Queixa}. Duração relatada: {Duracao}. Alergias relatadas: {Alergias}. Medicamentos relatados: {Medicamentos}. Observações: {Observacoes}.";
    }
}

public class MensagemMongo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Remetente { get; set; } = "Bot"; // "Bot" ou "Paciente"

    public string Conteudo { get; set; } = string.Empty;

    public string Tipo { get; set; } = "Texto"; // "Texto" ou "Opcoes"

    public List<string>? Opcoes { get; set; }

    public string? ClienteMessageId { get; set; }

    public DateTime EnviadaEmUtc { get; set; } = DateTime.UtcNow;

    public static MensagemMongo Bot(string conteudo, params string[] opcoes) => new()
    {
        Remetente = "Bot",
        Conteudo = conteudo,
        Tipo = opcoes.Length > 0 ? "Opcoes" : "Texto",
        Opcoes = opcoes.Length > 0 ? [.. opcoes] : null,
    };

    public static MensagemMongo DoPaciente(string conteudo, string? clienteMessageId = null) => new()
    {
        Remetente = "Paciente",
        Conteudo = conteudo,
        Tipo = "Texto",
        ClienteMessageId = clienteMessageId,
    };
}
