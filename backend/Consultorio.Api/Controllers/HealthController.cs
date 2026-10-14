using Consultorio.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;

namespace Consultorio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(AppDbContext context, MongoDbContext mongoContext) : ControllerBase
{
    /// <summary>
    /// Verifica a saúde dos serviços e conexões com os bancos PostgreSQL e MongoDB.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        bool postgresOk = false;
        bool mongoOk = false;

        try
        {
            postgresOk = await context.Database.CanConnectAsync();
        }
        catch
        {
            postgresOk = false;
        }

        try
        {
            await mongoContext.Conversas.CountDocumentsAsync(new BsonDocument());
            mongoOk = true;
        }
        catch
        {
            mongoOk = false;
        }

        var status = (postgresOk && mongoOk) ? "Healthy" : "Degraded";

        return Ok(new
        {
            status,
            timestampUtc = DateTime.UtcNow,
            servicos = new
            {
                postgreSql = postgresOk ? "Online" : "Offline",
                mongoDb = mongoOk ? "Online" : "Offline"
            }
        });
    }
}
