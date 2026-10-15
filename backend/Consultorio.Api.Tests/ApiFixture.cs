using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit;

// Todas as classes compartilham um único banco semeado; execução sequencial evita interferência.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Consultorio.Api.Tests;

[CollectionDefinition(Nome)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nome = "api";
}

/// <summary>
/// Sobe PostgreSQL e MongoDB reais em containers, inicia a API (migrations + seed do DbInitializer)
/// e expõe clientes HTTP autenticados. É a rede de segurança da refatoração: congela o
/// comportamento observável pela borda HTTP.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").Build();
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "consultorio-tests-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync());

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgresConnection"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:MongoConnection"] = _mongo.GetConnectionString(),
            ["Storage:UploadDirectory"] = _uploads,
        };

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            foreach (var (chave, valor) in settings) b.UseSetting(chave, valor);
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings));
        });

        using var client = _factory.CreateClient();
        var health = await client.GetAsync("/api/health");
        var corpo = await health.Content.ReadAsStringAsync();
        if (!corpo.Contains("\"Healthy\""))
            throw new InvalidOperationException("API não conectou aos containers: " + corpo);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask());
        if (Directory.Exists(_uploads)) Directory.Delete(_uploads, recursive: true);
    }

    public string MongoConnectionString => _mongo.GetConnectionString();
    public const string MongoDatabaseName = "consultorio_mongo_db";

    public HttpClient Anonimo() => _factory.CreateClient();

    public async Task<HttpClient> ComoAsync(string email, string senha = Seed.Senha)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, senha });
        resp.EnsureSuccessStatusCode();
        var token = (await resp.Json())["token"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

/// <summary>Identificadores fixos criados por <c>DbInitializer.SeedAsync</c>.</summary>
public static class Seed
{
    public const string Senha = "Senha@123";
    public const string Admin = "admin@consultorio.local";
    public const string MedicoA = "medico.santamaria@consultorio.local";
    public const string EnfermeiroA = "enfermeiro.santamaria@consultorio.local";
    public const string AtendenteA = "atendente.santamaria@consultorio.local";
    public const string MedicoB = "medico.saobento@consultorio.local";
    public const string EnfermeiroB = "enfermeiro.saobento@consultorio.local";
    public const string AtendenteB = "atendente.saobento@consultorio.local";
    public const string Carlos = "paciente.carlos@consultorio.local";
    public const string Mariana = "paciente.mariana@consultorio.local";

    public static readonly Guid InstA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid InstB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid MedicoAId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    public static readonly Guid MedicoBId = Guid.Parse("cccccccc-2222-2222-2222-222222222222");
    public static readonly Guid PacienteCarlos = Guid.Parse("10101010-1111-1111-1111-111111111111");
    public static readonly Guid PacienteMariana = Guid.Parse("10101010-2222-2222-2222-222222222222");
    public static readonly Guid ColetaCarlos = Guid.Parse("44444444-1111-1111-1111-111111111111");
    public static readonly Guid SlotPassadoCarlos = Guid.Parse("55555555-1111-1111-1111-111111111111");
    public static readonly Guid ConsultaCarlos = Guid.Parse("66666666-1111-1111-1111-111111111111");
    public static readonly Guid DocumentoCarlos = Guid.Parse("88888888-1111-1111-1111-111111111111");
    public static readonly Guid AutorizacaoCarlos = Guid.Parse("99999999-1111-1111-1111-111111111111");
}

public static class HttpJson
{
    public static async Task<JsonNode> Json(this HttpResponseMessage resp)
        => JsonNode.Parse(await resp.Content.ReadAsStringAsync())
           ?? throw new InvalidOperationException("resposta sem corpo JSON");

    public static async Task<JsonArray> JsonArray(this HttpResponseMessage resp) => (await resp.Json()).AsArray();

    /// <summary>Corpo padrão de erro da API: <c>{ "mensagem": "..." }</c>.</summary>
    public static async Task<string> Mensagem(this HttpResponseMessage resp)
        => (await resp.Json())["mensagem"]!.GetValue<string>();

    public static string S(this JsonNode? n, string prop) => n![prop]!.GetValue<string>();
    public static Guid G(this JsonNode? n, string prop) => n![prop]!.GetValue<Guid>();
    public static int I(this JsonNode? n, string prop) => n![prop]!.GetValue<int>();
    public static bool B(this JsonNode? n, string prop) => n![prop]!.GetValue<bool>();
    public static DateTime D(this JsonNode? n, string prop) => n![prop]!.GetValue<DateTime>();

    public static MultipartFormDataContent Multipart(string nome, string contentType, string conteudo)
        => Multipart(nome, contentType, System.Text.Encoding.UTF8.GetBytes(conteudo));

    public static MultipartFormDataContent Multipart(string nome, string contentType, byte[] bytes)
    {
        var arquivo = new ByteArrayContent(bytes);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { arquivo, "arquivo", nome } };
    }
}
