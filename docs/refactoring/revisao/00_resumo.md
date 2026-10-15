# Revisão de qualidade da branch `refactoring` (backend, resumo)

Esta é uma revisão independente, no padrão "thermo-nuclear", das mudanças de backend da branch `refactoring` em relação a `main`. Ela julga a estrutura do código novo, verifica se o comportamento foi de fato preservado e avalia a rede de testes que sustenta essa alegação. A evidência completa (comandos, saídas, mutantes, sondas e esboços de código) está nos arquivos `01` a `03` desta pasta.

## Escopo e método

A revisão cobre os commits `304637e` a `d483f38` (`git log --oneline main..HEAD`). Os dois commits mais recentes da branch (`8b9c114` e `be318ad`) chegaram durante a revisão e só mexem em frontend e documentação (`git diff --stat d483f38..HEAD` não lista nenhum arquivo de `backend/`), então o backend revisado é idêntico ao de `HEAD`. Li por inteiro os 27 arquivos C# de `backend/Consultorio.Api` (sem `Migrations/`), o `.csproj`, os `appsettings`, as versões originais em `main` dos arquivos alterados e os 9 arquivos do projeto de testes.

Além da leitura, rodei cinco tipos de verificação. A primeira rodou no repositório; as outras quatro, em cópias exportadas com `git archive` para o diretório temporário da sessão, sem alterar nenhum arquivo versionado:

1. A suíte de `HEAD` no repositório: 51/51 aprovados.
2. O pin retroativo: os testes de caracterização de `HEAD` contra o código original (`43eef9b`, que é `main` mais `partial class Program`): 33/37, e as 4 falhas são exatamente as mudanças documentadas em `TratamentoDeErrosTests`.
3. Sondas HTTP descartáveis, rodadas nas duas versões e comparadas com `diff`, para caminhos que o pin não cobre.
4. Dois mutantes na regra de consentimento, para medir o que o pin detecta.
5. Um experimento com um filtro de exceção do MVC no lugar do `IExceptionHandler`.

## Veredito

**Pedir mudanças.** A branch é uma melhoria líquida grande sobre `main`. O código caiu de 4.267 para 3.289 linhas. Os 30 `catch` viraram 3, que ficaram no health check e no seed. As 17 comparações `UsuarioId ==` usadas para redescobrir o solicitante no banco sumiram; as 2 restantes são subconsultas da projeção de usuários. Os dois N+1 sumiram, as opções passaram a ser tipadas e validadas na subida, e o roteiro da triagem virou uma função pura com 14 testes de unidade. Pela régua da skill, porém, dois itens são bloqueantes:

- uma regressão estrutural de fronteira, a política de exceções aplicada à API inteira usando tipos genéricos da BCL (achado 1);
- a ausência de pin para a regra de consentimento que a refatoração reescreveu (achado T1).

A branch também deixa passar o code-judo que o próprio diagnóstico recomendou para o mapeamento de DTOs (achado 2). Esse não é bloqueante, mas é a maior oportunidade restante. Nenhum arquivo chega perto de 1000 linhas, e o maior é `ProntuarioService.cs`, com 358.

## Números que sustentam o veredito

| Medida | `main` | `HEAD` | Fonte |
| --- | ---: | ---: | --- |
| Linhas C# sem `Migrations` | 4.267 | 3.289 | `medir.sh`, em `01` |
| Arquivos C# | 18 | 27 | idem |
| Cláusulas `catch` | 30 | 3 | idem |
| `.Include(` | 58 | 38 | idem |
| `new XxxDto(` fora de `Mapeamentos.cs` | 33 | 10 | idem |
| Comparações `UsuarioId ==` (em `HEAD`, só na projeção de usuários) | 17 | 2 | idem |
| `[Authorize(Roles = "...")]` com string solta | 18 | 18 | idem |
| Logs `fail:` numa execução dos 37 testes HTTP | 4 | 45 | `02`, seção 3 |
| Mutantes na regra de consentimento que sobrevivem ao pin | n/a | 2 de 2 | `03`, seção 4 |
| Linhas de sonda com comportamento diferente entre base e `HEAD` | n/a | 4 de 37 | `02`, seção 2 |

## Achados, na ordem de prioridade da skill

### Regressões estruturais

**1. O tratador global usa exceções genéricas da BCL como sinal de regra de negócio, agora para a API inteira, e registra cada 4xx como erro.** `TratadorGlobalDeExcecoes.cs:14-20` traduz qualquer `InvalidOperationException` em 400 e qualquer `UnauthorizedAccessException` em 403, com `excecao.Message` no corpo. Antes, esse mapeamento valia só nas ações que tinham `try/catch`; agora vale em todo o pipeline. Por isso, falhas de infraestrutura chegam ao cliente como se fossem regra de negócio:

- Com `MongoDatabaseName` ausente, a exceção de `MongoDbContext.cs:14-15` é engolida pelo `try/catch` do seed (`Program.cs:139-152`). Depois disso, `GET /api/health` responde `400 {"mensagem":"MongoDatabaseName não configurado."}`. Medi isso numa sonda.
- Com o diretório de upload sem permissão de escrita, `File.Create` (`ArmazenamentoArquivos.cs:20`) lança a `UnauthorizedAccessException` do .NET, e a API responde 403 com o caminho absoluto do servidor na mensagem.

Esses dois tipos também são lançados pelo EF Core, pela injeção de dependência e pelo `FirstAsync`. Há ainda o custo operacional: o `ExceptionHandlerMiddleware` do .NET 8 registra cada exceção como `fail` com stack trace, mesmo quando o tratador a resolve. Uma execução dos 37 testes HTTP produziu 45 entradas de erro em `HEAD` e 4 no código original. O remédio tem duas partes independentes. A primeira é criar tipos de domínio (`RegraVioladaException`, `AcessoNegadoException`, `ConflitoException`) e mapear só esses; as 34 ocorrências de `new InvalidOperationException(` e as 29 de `new UnauthorizedAccessException(` em código de aplicação mudam por substituição mecânica. A segunda é trocar o `IExceptionHandler` por um `IExceptionFilter` do MVC. Fiz esse experimento numa cópia: 51/51 aprovados e 0 linhas `fail:` no log. Detalhes em `01`, seção 2, e `02`, seção 3.

### Code-judo que ficou de fora

**2. O mapeamento por `ToDto()` em entidades mudou a complexidade de lugar sem apagá-la, e a projeção por `Expression` apagaria uma categoria inteira de código.** `Mapeamentos.cs:5-8` declara o contrato oculto: "as navegações lidas aqui precisam estar carregadas (Include) ou atribuídas pelo chamador". Esse contrato gerou três mecanismos que só existem para satisfazê-lo:

- `Consultas.ComDetalhes()` (`Data/Consultas.cs:9-16`), uma lista de `Include` cujo comentário aponta para o DTO;
- atribuições de navegação antes do `Add`, inclusive com `!` mentiroso (`ProntuarioService.cs:204`, `287`, `289`);
- 26 dos 38 `Include` restantes, que só alimentam um DTO ou um filtro feito em memória.

A mesma escolha impede o SQL de calcular o que hoje é calculado em memória. `ListarSlotsAsync` ainda carrega todas as consultas de todos os slots para decidir `disponivel` (`AgendaService.cs:27` e `50`). `ListarPacientesAsync` regrediu de uma projeção SQL em `main` para materializar entidades e mapear em memória (`AuxiliaryServices.cs:178-182`). Uma projeção `Expression<Func<Consulta, ConsultaDto>>` por DTO, usada com `Select`, elimina `ComDetalhes`, as atribuições de navegação e os `!`. Na escrita, a mesma projeção é reaplicada sobre o id recém-salvo. Há uma ressalva que precisa ser tratada de propósito: o histórico pina `temPesquisaRespondida = false` (`ProntuarioTests.cs:30-32`), um bug que depende de faltar um `Include`. Uma projeção calcularia o valor correto, então essa rota precisa de `with { TemPesquisaRespondida = false }` explícito até a correção do bug ser aprovada. Esboço em `01`, seção 3.

**3. `UsuarioAtual` virou um saco de parâmetros, e a política de acesso continua copiada em cada serviço.** O record (`UsuarioAtual.cs:10`) substituiu bem os pares `(usuarioId, perfil)`, mas a regra "o recurso pertence ao solicitante" continua escrita à mão 14 vezes em quatro serviços. São 8 na forma condicional `solicitante.Perfil == PerfilUsuario.Paciente && x.UsuarioId != solicitante.Id` (por exemplo `AgendaService.cs:101`, `214` e `240`, `AuxiliaryServices.cs:189` e `222`), 5 na forma incondicional para paciente e 1 para o médico responsável (`AgendaService.cs:248`). A política clínica também segue em dois métodos com formatos diferentes. No histórico, é um `switch` que monta `podeVerOrigem` (`ProntuarioService.cs:127-153`). No download, são `if` encadeados (`ProntuarioService.cs:227-255`). A guarda de Atendente/Admin aparece duas vezes (`110` e `227`), e a consulta de vínculo médico-paciente também (`141` e `247`). Mover a regra de dono para `UsuarioAtual` (por exemplo `solicitante.ExigirDono(paciente.UsuarioId, "Acesso negado.")`, com a mensagem como parâmetro porque o pin congela cada texto) e extrair uma política clínica única reduz as 14 cópias a 14 chamadas de uma linha com a regra num só lugar. Esboços em `01`, seção 4.

### Espaguete e ramificação

**4. Ainda há regra de negócio nos controllers, com valor sentinela e respostas divergentes para a mesma condição.** São três casos:

- `ColetasController.ListarFila` decide quem pode ver a fila e passa `Guid.Empty` como sentinela para o Admin sem instituição (`DomainControllers.cs:110-115`).
- `SlotsController.Criar` escolhe a instituição com `?? dto.InstituicaoId` (`162`).
- `ColetasController.Iniciar` responde 401 para "paciente sem registro de Paciente" (`98-101`), enquanto a mesma condição em `NotificacaoPesquisaService` vira 403 (`AuxiliaryServices.cs:84-85`). A sonda confirma: 401 em `POST /api/coletas` e 403 em `GET /api/notificacoes` para o mesmo usuário.

Essas regras preservam o comportamento e não devem mudar agora, mas pertencem ao serviço, que já recebe `UsuarioAtual`. O retorno `bool` de `ValidarColetaAsync` (`DomainControllers.cs:139-145`) pode virar exceção de regra com o mesmo corpo `{ mensagem }`. Os de `AtualizarStatusConsultaAsync` e `RevogarAutorizacaoAsync` devem ficar como estão, porque o pin congela a resposta ProblemDetails do `BadRequest()` vazio (`TriagemAgendamentoTests.cs:259`). Detalhes em `01`, seção 5.

### Fronteiras, abstrações e contratos de tipo

**5. Os contratos que cruzam fronteiras continuam como strings soltas.** São cinco frentes:

- **Claims.** Os nomes `"instituicaoId"`, `"pacienteId"` e `"medicoId"` estão duplicados entre quem emite (`JwtTokenGenerator.cs:33-35`) e quem lê (`UsuarioAtual.cs:24-26`). O claim `perfil` (`JwtTokenGenerator.cs:30`) perdeu o único leitor no commit `75f3a92`, e o frontend não decodifica o token (nenhuma ocorrência de `atob`, `jwt` ou `decode` em `frontend/src`).
- **Papéis.** Os 18 `Roles = "..."` são strings, com 7 combinações distintas.
- **Mensagens do Mongo.** O remetente e o tipo são `"Bot"`, `"Paciente"`, `"Texto"` e `"Opcoes"` (`ConversaMongo.cs:51-76`).
- **Etapas do roteiro.** São inteiros mágicos de 1 a 9 (`RoteiroTriagem.cs:24-79`).
- **Configuração do Mongo.** `MongoDbContext` ainda lê `IConfiguration` com `?? throw` (`MongoDbContext.cs:12-15`), em vez de seguir o padrão de opções validadas na subida que a branch criou para JWT (`Program.cs:17`).

Cada frente tem um remédio de poucas linhas que mantém o JSON e o armazenamento idênticos:

- um único tipo que emite e lê os claims;
- um atributo `[Perfis(PerfilUsuario.X, ...)]` derivado de `AuthorizeAttribute`;
- enums com `JsonStringEnumConverter`, que já é global (`Program.cs:97`), e `BsonRepresentation(String)`;
- `enum EtapaTriagem` armazenado como inteiro, com o DTO mantendo `int`;
- `MongoOptions` com `ValidateOnStart`.

O retorno em tupla `(byte[] Bytes, string ContentType, string NomeArquivo)?` (`ProntuarioService.cs:18`) pede um record nomeado. Detalhes em `01`, seção 6.

### Tamanho e decomposição

**6. Os arquivos-gaveta continuam e já ficaram baratos de dividir.** `DomainControllers.cs` tem 282 linhas com 11 controllers, e `AuxiliaryServices.cs` tem 272 com 5 serviços sem relação entre si. Esse era o passo 9 da sequência do diagnóstico, que pedia para dividir depois de encolher. O encolhimento aconteceu e a divisão não. `ProntuarioService.cs` (358 linhas, 9 métodos públicos) junta quatro assuntos: atendimento (evolução e conclusão), histórico, documentos e autorizações. `DbInitializer.SeedAsync` ainda é um método único de 250 linhas (`DbInitializer.cs:15-264`), aceitável para dados declarativos. Nenhum arquivo passa de 1000 linhas. Detalhes em `01`, seção 7.

### Modularidade

**7. Há abstrações que não pagam o custo e um ciclo entre pastas.** As 10 interfaces de implementação única somam 66 linhas, e nenhum teste usa um dublê delas (o pin é HTTP e os testes de unidade usam a classe estática `RoteiroTriagem`). `Data/Usuarios.cs` mistura uma projeção de leitura que recebe o `AppDbContext` como parâmetro (`Usuarios.cs:14-23`) com um helper de escrita sobre `UserManager` (`26-56`). O parâmetro `db` existe porque o modelo trata `Paciente`/`Medico` → `Usuario` como 1:N (`WithMany()` em `AppDbContextModelSnapshot.cs:745-749` e `775-779`) apesar do índice único. O helper descarta o `IdentityResult` de `AddToRoleAsync` (`Usuarios.cs:54`). `Infraestrutura` depende de `Services` (`TratadorGlobalDeExcecoes.cs:1`, por causa de `ConflitoException`), e `Services` depende de `Infraestrutura` (por causa de `UsuarioAtual`). As exceções e o solicitante são conceitos de domínio e pedem uma pasta própria. A promessa de "um ponto de montagem por DTO" também ficou parcial: `ConversaDetalheDto` e `ColetaItemFilaDto` são montados em `TriagemChatService.cs:147-156` e `215-241`, e `UsuarioInfoDto` em `Data/Usuarios.cs`. Detalhes em `01`, seção 8.

### Legibilidade

**8. Restam alguns detalhes menores.** Os 11 controllers repetem `[ApiController]` e `[Route("api/[controller]")]`, que poderiam subir para a base abstrata (`DomainControllers.cs:12-17`; confirmar com o pin, porque `AdminController` usa `api/admin`). O cache `_solicitante ??=` não tem efeito prático. `Paciente.TemAutorizacaoAtiva` (`Entities.cs:80-84`) está no lugar certo, pois o consentimento pertence ao paciente, mas devolve `false` em silêncio se `Autorizacoes` não tiver sido carregado. Como isso falha fechado, é aceitável, desde que a pré-condição fique documentada.

## Riscos de comportamento e qualidade do pin

**T1 (bloqueante). A regra de consentimento reescrita não tem pin.** Substituí `TemAutorizacaoAtiva` por `=> false` numa cópia e a suíte deu 51/51. Fiz o mesmo com o ramo médico de `podeVerOrigem` trocado por `_ => true`, e o resultado foi de novo 51/51. A causa é que nenhum teste cria uma consulta de um médico com paciente de outra instituição: `ProntuarioTests.cs:169-178` é negado antes, na checagem de vínculo. Minhas sondas exercitaram esse ramo, com autorização ativa e depois revogada, e as respostas de base e `HEAD` são idênticas. O comportamento está preservado, mas a suíte não sabe disso. O mesmo vale para outras 5 rotas cujo status mudou de 500 para 400 ou 403, além das 4 documentadas: histórico, documentos e autorizações de paciente inexistente, notificações e `GET` da pesquisa para um usuário Paciente sem registro. O commit `03f54ed` fala em 4 endpoints, mas são 9, como o diagnóstico previu (`01_backend_controllers_e_erros.md:41-53`). Detalhes em `02`, seções 2 e 4, e `03`, seção 5.

**Os riscos que o enunciado pediu para checar estão, na maioria, seguros.** A tabela completa está em `02`, seção 1. Em resumo:

- O fixup de navegação no histórico funciona.
- `FindAsync(solicitante.MedicoId)` com `null` devolve `null` e produz o mesmo 403 "Médico não encontrado." da base. A sonda P1 confirma.
- `Add` no lugar de `AddAsync` é indiferente com chaves `Guid` inicializadas.
- Os `Include` removidos não eram lidos.
- O `GroupBy` é traduzido e está pinado com contagens exatas.
- O `ToDictionary` por `_id` do Mongo é seguro.
- A ordem e o conteúdo da lista de usuários do admin são idênticos (sonda P3).

Há duas mudanças pequenas fora das mensagens de commit. O nome do médico na evolução agora vem do autor da evolução, não do médico do slot, e os dois são iguais por invariante (`ProntuarioService.cs:39`). E, no ambiente Development usado pelos testes, as respostas 500 trocaram a página de exceção em texto puro por ProblemDetails (sonda P4); isso só está registrado no comentário de `Program.cs:79`.

**O pin testa comportamento, cobre as 36 rotas e passou no teste retroativo, mas tem pontos frágeis.** O pin congela status e mensagens pela borda HTTP, como deve. Rodado contra o código original, só falham os 4 testes de mudança declarada, o que prova que os testes acrescentados durante a refatoração descrevem o comportamento antigo. Os pontos frágeis são três:

- As contagens exatas do dashboard dependem de nenhum outro teste criar consultas ou coletas pendentes nas instituições A e B (`TriagemAgendamentoTests.cs:364-383`).
- `Slots_filtram_por_data` passa sem verificar nada quando amanhã é sábado ou domingo, porque o `Assert.All` roda sobre uma lista vazia (`CatalogoTests.cs:71-80`).
- O cenário `Fluxo_completo_da_triagem_ao_dashboard` tem 366 linhas num único `[Fact]` (`TriagemAgendamentoTests.cs:18-384`). Dividir não custa garantias de ordem se cada teste criar o próprio paciente via `POST /api/pacientes`.

Detalhes em `03`.

## Sequência de remediação proposta

1. **Fechar o pin antes de mexer de novo.** Transformar as sondas de `02` em testes: consentimento positivo e revogado, download do médico da própria instituição, as 5 rotas não pinadas que saíram de 500, os usuários com perfil sem registro e a validação reprovada. Rodar contra `43eef9b` e `HEAD`, como fiz aqui.
2. **Fronteira de exceções.** Criar tipos de domínio e trocar para um `IExceptionFilter` do MVC; o experimento já mostrou 51/51 e 0 logs de erro. As exceções de infraestrutura voltam a ser 500.
3. **Mover a regra de dono e os claims para `UsuarioAtual`.** Uma única fonte para os nomes de claim e um método de exigência de dono. Remover o claim `perfil` é opcional, porque muda o conteúdo do token, mesmo sem nenhum leitor.
4. **Projeções por `Expression` nos caminhos de leitura**, com a ressalva do histórico. Apagar `ComDetalhes` e os `!`, e trocar o `Include(s => s.Consultas)` de `ListarSlotsAsync` por um `EXISTS`.
5. **Política clínica única e divisão de `ProntuarioService`** em atendimento, histórico, documentos e autorizações.
6. **Contratos tipados**: `[Perfis]`, enums de mensagem do Mongo, `EtapaTriagem` e `MongoOptions`.
7. **Divisão física de arquivos** (um tipo por arquivo) e remoção das interfaces de implementação única.
8. **Higiene do pin**: um paciente novo por teste, asserções de dashboard por diferença, `Assert.NotEmpty` no filtro por data e o cenário quebrado em fases.

## Arquivos de detalhe

- `01_estrutura_e_code_judo.md`: medições antes e depois, comandos, achados 1 a 8 com esboços de antes e depois.
- `02_riscos_de_comportamento.md`: tabela de riscos com veredito, sondas comparando base e `HEAD`, sondas de configuração e de permissão de disco, contagem de logs, experimento do filtro MVC e código das sondas.
- `03_testes.md`: estrutura e números da suíte, pin retroativo, mutantes, cobertura de rotas e de ramos, fragilidades e proposta de divisão do cenário.
