# 03. Backend: configuração, modelo de dados e seed

Linha de base: commit `8ff4b1d` (branch `main`). Caminhos relativos a `backend/Consultorio.Api/`, salvo indicação. A pasta `Migrations/` foi ignorada, como pedido.

## 1. Medições

| Arquivo | Linhas | Observação |
| --- | ---: | --- |
| `Data/DbInitializer.cs` | 434 | um único método, `SeedAsync`, com 422 linhas (`12-433`) |
| `Models/Entities/Entities.cs` | 327 | 13 classes de entidade no mesmo arquivo |
| `Program.cs` | 172 | instruções de nível superior, 9 seções numeradas por comentário |
| `Data/AppDbContext.cs` | 135 | `OnModelCreating` com 110 linhas (`25-134`) |
| `Models/Mongo/ConversaMongo.cs` | 65 | 3 classes |
| `Data/MongoDbContext.cs` | 25 | lê configuração por conta própria |

Comandos:

```bash
wc -l Program.cs Data/*.cs Models/*/*.cs appsettings*.json Consultorio.Api.csproj
grep -rnE 'onfiguration(\[|\.GetConnectionString)' --include='*.cs' . | grep -v Migrations   # 14 linhas
grep -c "\[ForeignKey" Models/Entities/Entities.cs     # 23
grep -c "HasForeignKey" Data/AppDbContext.cs           # 13
grep -c "await userManager.CreateAsync" Data/DbInitializer.cs   # 9
grep -c "AddToRoleAsync" Data/DbInitializer.cs                  # 9
grep -n  "slotsFuturos.Add(new SlotAgenda" Data/DbInitializer.cs # 389, 398, 411, 420
grep -rn "BCrypt" --include='*.cs' . | wc -l                     # 0
grep -rn "WithOpenApi\|AddOpenApi\|MapOpenApi" --include='*.cs' . | wc -l   # 0
```

## 2. Configuração lida em quatro lugares, com nomes e padrões diferentes

Há 14 linhas que leem configuração, espalhadas por `Program.cs`, `Data/MongoDbContext.cs`, `Services/JwtTokenGenerator.cs` e `Services/ProntuarioService.cs`, quase sempre com `IConfiguration[...]` e uma cadeia de `??`. A mesma configuração lógica é lida em dois lugares com regras diferentes:

| Configuração | Onde é lida | Chaves consultadas, em ordem | Padrão no código |
| --- | --- | --- | --- |
| Conexão PostgreSQL | `Program.cs:15-16` | `ConnectionStrings:PostgresConnection` | senha `postgrespassword` |
| Conexão MongoDB | `MongoDbContext.cs:12-14` | `ConnectionStrings:MongoConnection`, `MONGO_CONNECTION_STRING` | `mongodb://localhost:27017` |
| Banco MongoDB | `MongoDbContext.cs:16-18` | `MongoDatabaseName`, `MONGO_DATABASE_NAME` | `consultorio_mongo_db` |
| Chave JWT para validar | `Program.cs:37-38` | `Jwt:SecretKey` | `CHAVE_SECRETA_SUPER_LONGA_POC_...` |
| Chave JWT para assinar | `JwtTokenGenerator.cs:25-27` | `JWT_SECRET_KEY`, `Jwt:SecretKey` | `CHAVE_SECRETA_PADRAO_POC_...` (outro texto) |
| Issuer e audience para validar | `Program.cs:39-40` | `Jwt:Issuer`, `Jwt:Audience` | `ConsultorioApi`, `ConsultorioApp` |
| Issuer e audience para assinar | `JwtTokenGenerator.cs:29-30` | `JWT_ISSUER` ou `JWT_AUDIENCE`, depois `Jwt:*` | os mesmos |
| Expiração do token | `JwtTokenGenerator.cs:31` | só `JWT_EXPIRATION_HOURS` | 24 |
| Pasta de upload | `ProntuarioService.cs:32` | `Storage:UploadDirectory` | `./uploads` |

Três consequências, todas verificáveis lendo os arquivos:

A primeira é que validação e assinatura do JWT não leem a mesma fonte. Se a variável `JWT_SECRET_KEY` existir no ambiente (ela está documentada em `.env.example:7`), o token é assinado com ela, mas validado com `Jwt:SecretKey` de `appsettings.json:16`. Os dois padrões embutidos no código também são textos diferentes (`Program.cs:38` e `JwtTokenGenerator.cs:27`). Hoje isso não aparece porque ninguém define a variável; é um defeito latente, listado no resumo.

A segunda é que a chave `Jwt:ExpirationHours` de `appsettings.json:19` nunca é lida. A expiração só vem de `JWT_EXPIRATION_HOURS`; o valor 24 coincide com o padrão por acaso.

A terceira é que `.env.example` documenta nomes que o código ignora ou que nunca vencem. `POSTGRES_CONNECTION_STRING` (`.env.example:2`) e `STORAGE_UPLOAD_DIRECTORY` (`.env.example:13`) não são lidas em lugar nenhum. `MONGO_CONNECTION_STRING` e `MONGO_DATABASE_NAME` só são consultadas depois das chaves do `appsettings.json`, que sempre existem. Além disso, o .NET não carrega arquivos `.env` sozinho; a convenção nativa para sobrescrever por ambiente é `Jwt__SecretKey`, `ConnectionStrings__PostgresConnection` e assim por diante, que o provedor de variáveis de ambiente já mapeia para `Jwt:SecretKey`.

Há ainda uma divergência de valor: `appsettings.json:11` usa `Password=postgres`, enquanto `docker-compose.yml:11` e o padrão de `Program.cs:16` usam `postgrespassword`. Como o `appsettings.json` tem a chave, o padrão nunca é usado, e quem sobe o banco pelo `docker-compose.yml` recebe uma senha que a API não usa. Também está no resumo como defeito.

### Proposta code-judo: uma classe de opções por assunto, lida uma vez

```csharp
// Configuracao/JwtOptions.cs
public sealed class JwtOptions
{
    public string SecretKey { get; init; } = "";
    public string Issuer { get; init; } = "ConsultorioApi";
    public string Audience { get; init; } = "ConsultorioApp";
    public int ExpirationHours { get; init; } = 24;
}

// Program.cs
var jwt = builder.Configuration.GetRequiredSection("Jwt").Get<JwtOptions>()!;
builder.Services.AddSingleton(jwt);   // o gerador recebe JwtOptions em vez de IConfiguration
// AddJwtBearer usa jwt.SecretKey, jwt.Issuer e jwt.Audience
```

O mesmo vale para `StorageOptions` (pasta de upload, limite de 5 MB e extensões aceitas, hoje literais em `ProntuarioService.cs:328` e `333`) e `MongoOptions`. Com isso desaparecem as 14 linhas de leitura dispersas, os dois segredos padrão diferentes e os nomes alternativos de variáveis. Preservação de comportamento: no cenário normal de desenvolvimento, sem variáveis de ambiente, todas as configurações efetivas continuam as mesmas, inclusive a expiração de 24 horas. O único cenário que muda é o que hoje está quebrado (variável `JWT_SECRET_KEY` definida). O `.env.example` precisa ser reescrito com os nomes no formato `Secao__Chave`.

## 3. Program.cs

`Program.cs` tem 172 linhas de instruções de nível superior. O bloco de Swagger ocupa 35 linhas (`97-131`) e a inicialização do banco ocupa 18 (`136-153`). Esse último bloco captura qualquer exceção do seed, escreve no console e segue em frente (`149-152`, sinalizado pelo analisador como `CA1031`). Na prática, se o PostgreSQL estiver inacessível ou a senha estiver errada (seção 2), a API sobe e passa a responder 500 em toda rota que toca o banco, sem falhar na partida. A especificação pede "falhas de dependência retornam erro explícito e recuperável" (`Context_Prompts/prompt-poc-clinica-instrucoes-agent.md`, seção 9); falhar na partida, ou ao menos refletir o problema no health check, atende melhor a isso. As origens de CORS estão fixas em `Program.cs:82`.

Depois do tratador de erros (`01_backend_controllers_e_erros.md`), das opções acima e dos health checks nativos, `Program.cs` se reorganiza quase sozinho. Extrair métodos de extensão (`AddAutenticacaoJwt`, `AddSwaggerComJwt`) só vale a pena se o arquivo voltar a crescer; com 172 linhas, não é prioridade.

## 4. Modelo relacional

`AppDbContext.OnModelCreating` (`AppDbContext.cs:25-134`, 110 linhas) configura 13 relacionamentos com `HasOne`, `WithMany` e `HasForeignKey`. As entidades declaram as mesmas chaves com 23 atributos `[ForeignKey]` (`Entities.cs`, por exemplo `18`, `58`, `88`, `93`). O mesmo mapeamento existe em dois lugares. O que de fato não é convenção no `OnModelCreating` são os 13 `OnDelete` (`Restrict` ou `Cascade`), os 5 índices únicos e o filtro do índice parcial. O restante repete o que o EF já deduziria.

O filtro do índice parcial usa um número mágico explicado por comentário:

```csharp
// AppDbContext.cs:51-54
builder.Entity<Consulta>()
    .HasIndex(c => c.SlotId)
    .HasFilter("\"Status\" != 4") // StatusConsulta.Cancelada = 4
    .IsUnique();
```

`HasFilter($"\"Status\" != {(int)StatusConsulta.Cancelada}")` gera o mesmo SQL, portanto não muda o snapshot de migrations, e deixa de depender do comentário.

Remédio para a duplicação: manter no `OnModelCreating` apenas o que não é convenção (comportamentos de exclusão, índices e filtro) e remover os `[ForeignKey]` redundantes ou, na direção inversa, remover as linhas de fluent API que só repetem a convenção. A prova de que o esquema não mudou é objetiva: depois da alteração, `dotnet ef migrations add VerificacaoSemMudanca` deve gerar uma migration com `Up` e `Down` vazios, que então é descartada. Separar a configuração em classes `IEntityTypeConfiguration<T>` é opcional; com 13 entidades, um único método enxuto ainda se lê bem.

Outros pontos do modelo, de prioridade menor:

- `Entities.cs` reúne as 13 entidades num arquivo de 327 linhas. Um arquivo por entidade, ou por módulo, segue a mesma lógica de `DomainControllers.cs`.
- As coleções de navegação são `ICollection<T> { get; set; } = new List<T>()`; o analisador aponta 14 ocorrências de `CA2227`. É o padrão aceito pelo EF e não precisa mudar.
- `FinalidadeNotificacao.LembreteConsulta` e `FinalidadeNotificacao.StatusColeta` (`Models/Enums/Enums.cs:45-46`) nunca são usados; só `ConvitePesquisa` é gravado.
- `NotificacaoSimulada.LidaEmUtc` só é preenchido pelo seed (`DbInitializer.cs:359` e `371`); nenhuma rota marca notificação como lida.

## 5. MongoDbContext

`MongoDbContext` (`Data/MongoDbContext.cs:10-22`) recebe `IConfiguration` e resolve a própria conexão com fallbacks (seção 2). É registrado como singleton (`Program.cs:65`), o que está correto para `MongoClient`. O analisador aponta `CA2000` em `MongoDbContext.cs:20`, sem efeito prático porque o cliente vive o tempo todo da aplicação. A mudança útil aqui é receber `MongoOptions` em vez de `IConfiguration`.

## 6. Seed: 422 linhas em um método

`DbInitializer.SeedAsync` (`Data/DbInitializer.cs:12-433`) é o método mais longo do projeto. A maior parte é repetição:

- Nove usuários são criados com o mesmo padrão de três passos: um literal `new Usuario { ... }` com 7 ou 8 propriedades, `userManager.CreateAsync` e `userManager.AddToRoleAsync` (9 ocorrências de cada). O `IdentityResult` das 18 chamadas é descartado; se uma senha ou e-mail violar a política do Identity, o seed segue em silêncio e o usuário não existe.
- A geração de horários repete o literal `slotsFuturos.Add(new SlotAgenda { ... })` quatro vezes (`389`, `398`, `411`, `420`), duas por médico, dentro de um laço que redeclara `int[] horas` a cada dia (`386`).
- Os comentários de `385` ("08:00 às 11:00") e `408` ("09:00 às 17:00") descrevem horários que não são os gerados: os dois médicos recebem as horas 8, 9, 10, 14, 15 e 16 (`386`).
- A conversa do Carlos é gravada no MongoDB (`275-279`) antes de a coleta correspondente ser gravada no PostgreSQL (`432`), invertendo a ordem que a especificação define.
- A guarda de idempotência (`28-31`) verifica se existem instituições, que são gravadas logo no início (`54-55`). Uma falha em qualquer ponto depois da linha 55 deixa o seed pela metade, e as execuções seguintes nunca o completam.
- A conversa semeada fica com `EtapaAtual = 8` e `Status = "Concluida"` (`253-254`), um estado que o roteiro real nunca produz (ver `02_backend_services.md`, seção 3).
- O resumo validado é um texto escrito à mão (`239`) que imita o template de `ConversaMongo.cs:46`.

### Proposta code-judo: seed como tabela

```csharp
record UsuarioSeed(string Id, string Email, string Nome, PerfilUsuario Perfil, Guid? InstituicaoId);

static readonly UsuarioSeed[] Usuarios =
[
    new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "admin@consultorio.local", "Administrador do Sistema", PerfilUsuario.Admin, null),
    new("bbbbbbbb-1111-1111-1111-111111111111", "medico.santamaria@consultorio.local", "Dr. Carlos Roberto", PerfilUsuario.Medico, InstA),
    // ... os outros 7, um por linha
];

static async Task CriarUsuarioAsync(UserManager<Usuario> um, UsuarioSeed s, string senha)
{
    var u = new Usuario { Id = Guid.Parse(s.Id), UserName = s.Email, Email = s.Email, NomeCompleto = s.Nome,
                          Perfil = s.Perfil, InstituicaoId = s.InstituicaoId, EmailConfirmed = true };
    Garantir(await um.CreateAsync(u, senha));
    Garantir(await um.AddToRoleAsync(u, s.Perfil.ToString()));
}

// horários: o produto cartesiano substitui os quatro literais
var slots =
    from dia in Enumerable.Range(1, 7).Select(d => hoje.AddDays(d))
    where dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
    from medico in new[] { (Id: medA.Id, Inst: instA.Id), (Id: medB.Id, Inst: instB.Id) }
    from hora in new[] { 8, 9, 10, 14, 15, 16 }
    from minuto in new[] { 0, 30 }
    let inicio = dia.AddHours(hora).AddMinutes(minuto)
    select new SlotAgenda { MedicoId = medico.Id, InstituicaoId = medico.Inst, InicioUtc = inicio, FimUtc = inicio.AddMinutes(30) };
```

Os dados gerados são os mesmos: os mesmos ids fixos, os mesmos e-mails e os mesmos horários (cada hora gera os intervalos de :00 a :30 e de :30 a :00 da hora seguinte, como hoje em `394-395` e `403-404`). Só a ordem de inserção dos horários muda, e ela não é observável porque os ids dos horários futuros são aleatórios. A diferença deliberada é `Garantir`, que transforma uma falha silenciosa do Identity numa exceção; isso deve ser registrado. Estimativa: o arquivo cai de 434 para cerca de 150 linhas. A senha de demonstração `Senha@123` (`DbInitializer.cs:33`) também está fixa no frontend (`frontend/src/app/components/login/login.component.ts:212`); para uma POC de demonstração isso é aceitável, mas os dois lugares precisam mudar juntos.

## 7. Projeto e perfis de execução

- `Consultorio.Api.csproj:10` referencia `BCrypt.Net-Next` 4.0.3, que não é usado em nenhum arquivo `.cs` (o Identity faz o hash das senhas). É dependência morta; na versão de `main`, a DLL ainda ia para `bin/` versionado (ver `05_higiene_repositorio_e_docs.md`).
- `Consultorio.Api.csproj:13` referencia `Microsoft.AspNetCore.OpenApi` 8.0.20, sem nenhuma chamada a `WithOpenApi`, `AddOpenApi` ou `MapOpenApi`; quem gera o Swagger é o Swashbuckle (`csproj:20`). É resto do template e ainda está numa versão de patch diferente dos demais pacotes 8.0.11.
- O projeto não configura nível de análise (`AnalysisLevel`) nem trata avisos como erro. Com o padrão do SDK, o build termina com 0 avisos, e os problemas citados nestes relatórios só aparecem com `-p:AnalysisLevel=latest-all`. Ligar um nível moderado (por exemplo `latest-recommended`, com as regras de `ConfigureAwait` desligadas) ajuda a impedir regressões depois da refatoração.
- A porta da API tem três fontes: `Properties/launchSettings.json:17` e `27` (5240 e 7145), o README (`--urls "http://localhost:5000"`, linha 30) e o frontend (`http://localhost:5000` fixo em dois serviços). O perfil "IIS Express" (`launchSettings.json:32-39`) é resto de template.
