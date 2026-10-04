using Consultorio.Api.Models.Mongo;
using MongoDB.Driver;

namespace Consultorio.Api.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoConnection") 
            ?? configuration["MONGO_CONNECTION_STRING"] 
            ?? "mongodb://localhost:27017";

        var databaseName = configuration["MongoDatabaseName"] 
            ?? configuration["MONGO_DATABASE_NAME"] 
            ?? "consultorio_mongo_db";

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<ConversaMongo> Conversas => _database.GetCollection<ConversaMongo>("conversas");
}
