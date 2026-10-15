using Consultorio.Api.Models.Mongo;
using MongoDB.Driver;

namespace Consultorio.Api.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:MongoConnection não configurada.");
        var databaseName = configuration["MongoDatabaseName"]
            ?? throw new InvalidOperationException("MongoDatabaseName não configurado.");

        _database = new MongoClient(connectionString).GetDatabase(databaseName);
    }

    public IMongoCollection<ConversaMongo> Conversas => _database.GetCollection<ConversaMongo>("conversas");
}
