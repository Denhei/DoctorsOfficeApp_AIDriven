using System.Text;
using System.Text.Json.Serialization;
using Consultorio.Api.Data;
using Consultorio.Api.Infraestrutura;
using Consultorio.Api.Models.Entities;
using Consultorio.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurações de Conexão
var postgresConn = builder.Configuration.GetConnectionString("PostgresConnection") 
    ?? "Host=localhost;Port=5432;Database=consultorio_db;Username=postgres;Password=postgrespassword";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConn));

// 2. Configurações do Identity
builder.Services.AddIdentity<Usuario, Papel>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 3. Configurações do JWT
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] 
    ?? "CHAVE_SECRETA_SUPER_LONGA_POC_CONSULTORIO_2026_ACADEMICA_SEGURA_MIN_32_BYTES!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ConsultorioApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ConsultorioApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 4. Injeção de Dependências
builder.Services.AddExceptionHandler<TratadorGlobalDeExcecoes>();
builder.Services.AddProblemDetails(); // fallback (500 em application/problem+json) para exceções não mapeadas
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITriagemChatService, TriagemChatService>();
builder.Services.AddScoped<IAgendaService, AgendaService>();
builder.Services.AddScoped<IProntuarioService, ProntuarioService>();
builder.Services.AddScoped<INotificacaoPesquisaService, NotificacaoPesquisaService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IInstituicaoService, InstituicaoService>();
builder.Services.AddScoped<IPacienteService, PacienteService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// 5. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200", "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 6. Controllers & JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// 7. Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Consultorio API - POC Acadêmica",
        Version = "v1",
        Description = "API REST da POC acadêmica de triagem inicial e agendamento sem IA."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT no formato: Bearer {seu_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 8. Executar Migrations e Seed no Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<Usuario>>();
        var roleManager = services.GetRequiredService<RoleManager<Papel>>();
        var mongoContext = services.GetRequiredService<MongoDbContext>();

        await DbInitializer.SeedAsync(context, userManager, roleManager, mongoContext);
        Console.WriteLine(">>> Banco de dados inicializado e Seed aplicado com sucesso!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Erro ao inicializar banco de dados: {ex.Message}");
    }
}

// 9. Pipeline HTTP
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Consultorio API v1");
    });
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposto para o projeto de testes (WebApplicationFactory<Program>).
public partial class Program { }
