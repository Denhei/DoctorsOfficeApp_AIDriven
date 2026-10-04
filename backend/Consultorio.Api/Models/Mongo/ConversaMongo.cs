using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Consultorio.Api.Models.Mongo;

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

    public int VersaoRoteiro { get; set; } = 1;

    public string Status { get; set; } = "EmAndamento"; // EmAndamento, Concluida, Cancelada

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

    public string Tipo { get; set; } = "Texto"; // "Texto", "Opcoes", "Resumo"

    public List<string>? Opcoes { get; set; }

    public string? ClienteMessageId { get; set; }

    public DateTime EnviadaEmUtc { get; set; } = DateTime.UtcNow;
}
