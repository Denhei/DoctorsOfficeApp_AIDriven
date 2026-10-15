# Diagnóstico de dívida técnica do DoctorsOfficeApp (resumo)

Este é o resumo de uma auditoria de qualidade de código feita no estilo "thermo-nuclear": leitura integral do código, medição com comandos reproduzíveis e foco em reestruturações que apagam categorias inteiras de complexidade sem mudar o comportamento. A evidência completa, os comandos e os esboços de código estão nos arquivos `01` a `05` desta pasta.

## Escopo e linha de base

A linha de base é o commit `8ff4b1d` da branch `main`, o código original. Todas as medições foram feitas sobre uma exportação desse commit (`git archive main`), porque a branch `refactoring` recebeu um commit durante a auditoria (`415c865`, que removeu os artefatos de build e o arquivo `.http`). Ficaram de fora `Migrations/`, `bin/`, `obj/` e `node_modules/`.

O backend tem 18 arquivos C# com 4.267 linhas, numa API ASP.NET Core sobre `net8.0` com EF Core 8, MongoDB.Driver 3.1, Identity e JWT. O frontend tem 21 arquivos TypeScript com 2.961 linhas, mais 166 linhas de CSS global. Um detalhe de partida: o frontend é Angular 19 (`frontend/package.json:13-20`, travado em 19.2.25 em `frontend/package-lock.json:552`). Não é Angular 20 nem o Angular 16 que a documentação descreve. O backend compila com 0 avisos no modo padrão; com `-p:AnalysisLevel=latest-all`, os analisadores confirmam dois dos achados abaixo (`CA1031` e `CA1307`).

## Veredito

Pelo critério desta revisão, o código não seria aprovado como está. O motivo não é tamanho: nenhum arquivo chega a 1000 linhas (o maior, `Controllers/DomainControllers.cs`, tem 596). O motivo é que a complexidade está concentrada em poucos padrões, copiados muitas vezes. A política de erro HTTP está escrita 27 vezes. A identidade de quem faz a requisição é re-descoberta no banco em 17 pontos, embora já esteja no token. Nove DTOs são montados à mão em 24 lugares. O roteiro da triagem é um `switch` de 167 linhas com o texto das perguntas embutido e duplicado. No frontend, cada uma das 38 assinaturas de chamadas de API trata carregamento e erro por conta própria, de maneiras diferentes.

Cada um desses padrões tem uma reorganização direta que remove a repetição em vez de mudá-la de lugar. A arquitetura é plana (controller, serviço, `DbContext`), então essas mudanças são locais e baratas. O maior risco não está no código, está na ausência de qualquer teste. Por isso o primeiro passo da sequência proposta é criar uma rede de caracterização.

## Números que sustentam o diagnóstico

| Medida | Valor | Onde |
| --- | ---: | --- |
| Blocos `try` / cláusulas `catch` em controllers | 18 / 27 | `DomainControllers.cs` |
| Ações que devolvem 500 para erro de negócio esperado | 9 | `01`, seção 2 |
| Consultas ao banco para achar `Paciente`/`Medico` pelo usuário | 17 | 4 arquivos de `Services/` |
| Métodos de serviço que recebem `(solicitanteUsuarioId, perfil)` | 11 | `Services/` |
| Construções manuais de DTO (9 tipos) | 24 | `ConsultaDto` sozinho: 4 |
| Literais `new MensagemMongo`, todos com campos redundantes | 14 | `TriagemChatService.cs` |
| Método mais longo | 422 linhas | `DbInitializer.SeedAsync` |
| Maior método de domínio | 221 linhas | `TriagemChatService.ProcessarMensagemAsync` |
| Linhas que leem `IConfiguration`, quase todas com `??` em cascata | 14 | 4 arquivos |
| Strings de papel em `[Authorize(Roles = ...)]` | 18 | `DomainControllers.cs` |
| `alert(...)` / erros engolidos / chamadas sem tratamento de erro (frontend) | 11 / 10 / 13 | 14 componentes |
| Usos de `any` no frontend | 11 | `api.service.ts` e 4 componentes |
| Diretivas `*ngIf`/`*ngFor`/`*ngSwitchCase` legadas | 96 | 14 componentes e `app.component.ts` |
| Artefatos de build versionados em `main` | 141 arquivos, 37,0 MB | `backend/Consultorio.Api/bin` e `obj` |
| Arquivos de teste | 0 | repositório inteiro, em `main` |

## Achados, em ordem de impacto na manutenção

**1. A política de exceção para HTTP mora em cada ação.** `DomainControllers.cs` tem 18 blocos `try` e 27 `catch`, com 16 cópias literais de `StatusCode(403, new { mensagem = ex.Message })` e 11 de `BadRequest(new { mensagem = ex.Message })`. Como cada ação decide sozinha o que captura, nove delas deixam escapar exceções que os serviços lançam de propósito. Um exemplo é `NotificacoesController.Listar` (`DomainControllers.cs:547-548`), que devolve 500 para a `UnauthorizedAccessException` de `AuxiliaryServices.cs:30`. O status 409 é decidido lendo o texto da mensagem (`ex.Message.Contains("já foi reservado")`, `DomainControllers.cs:384`), o que acopla o controller à redação de `AgendaService.cs:162` e `175`. O remédio é um único `IExceptionHandler` do .NET 8 que mantém o corpo `{ mensagem }` lido pelo frontend, mais um tipo `ConflitoException`. Os 18 `try` desaparecem e cada ação fica com uma linha. Detalhes e a tabela das nove rotas em `01_backend_controllers_e_erros.md`, seção 2.

**2. Quem é o usuário é perguntado ao banco em vez de lido do token.** Os controllers extraem id e perfil (`GetUserId()` 23 vezes, `GetUserRole()` 13 vezes) e repassam o par a 11 métodos de serviço. Os serviços fazem 17 consultas para descobrir o `Paciente` ou o `Medico` correspondente, às vezes duas vezes no mesmo método (`ProntuarioService.cs:193` e `216`). O token já traz `pacienteId`, `medicoId` e `instituicaoId` (`JwtTokenGenerator.cs:47-60`); o claim `medicoId` nunca é lido pelo backend. A extração tem fallbacks mortos: `DomainControllers.cs:19` repete o valor de `ClaimTypes.NameIdentifier`. Ela também tem um padrão inseguro: `DomainControllers.cs:29` assume `Paciente` quando o claim falta. Um record `UsuarioAtual`, montado uma vez a partir dos claims, substitui os três helpers, os 11 pares de parâmetros e as 17 consultas (`01`, seção 3).

**3. O roteiro da triagem é um `switch` que mistura transição, preenchimento e texto.** `ProcessarMensagemAsync` (`TriagemChatService.cs:113-333`) tem 221 linhas, e 167 delas são o `switch` de `157-323`. Como cada pergunta é emitida no ramo de origem, as perguntas de medicamentos e de observações estão duplicadas palavra por palavra (`221` e `236`, `266` e `281`). A ramificação depende do texto da resposta (`Contains("Sim")` em `199` e `244`). A reorganização é indexar cada pergunta pela etapa de chegada e deixar num lugar só a função que aplica a resposta e devolve a próxima etapa. Assim cada pergunta existe uma única vez, por construção, e o método cai para cerca de 35 linhas (`02`, seção 2). No mesmo módulo, `ConversaMongo.Status` é gravado em quatro pontos (três no serviço e um no seed) e nunca lido. É uma cópia do status que já vive no PostgreSQL, contra o que a própria especificação pede. `VersaoRoteiro` e `EtapaEsperada` também são código morto. Removê-los exige `[BsonIgnoreExtraElements]` para não quebrar a leitura dos documentos já gravados (`02`, seção 3).

**4. DTOs montados à mão, sustentados por cadeias de `Include`.** São 24 construções posicionais para 9 tipos, com 58 chamadas `.Include(` nos serviços para garantir que as navegações estejam carregadas. A quarta versão do mapeamento de consulta (`ProntuarioService.cs:541-562`) usa fallbacks como `?? "Médico"` e `?? DateTime.MinValue`, que nunca disparam nos chamadores e só escondem o invariante. Uma projeção `Expression<Func<Consulta, ConsultaDto>>` por tipo, usada com `Select`, elimina os `Include` dos caminhos de leitura. Ela também transforma o cálculo de disponibilidade de horários, que hoje carrega todas as consultas de todos os slots (`AgendaService.cs:33` e `64`), num `EXISTS` no SQL (`02`, seção 4).

**5. A regra de acesso clínico está escrita três vezes.** `ObterHistoricoPacienteAsync` (`ProntuarioService.cs:163-310`, 148 linhas) autoriza, filtra, mapeia e monta. A filtragem são dois `foreach` espelhados (`220-238` e `240-258`) com o mesmo predicado de autorização ativa. O download (`386-439`) reimplementa a regra contra o banco, com o predicado pela terceira vez (`415-419`). Uma função "pode ver dado desta instituição", calculada uma vez, transforma 55 linhas em 12 e serve aos dois métodos (`02`, seção 5).

**6. Serviços de domínio fazendo trabalho de infraestrutura.** O construtor de `ProntuarioService` lê `IConfiguration` e cria diretório no disco (`ProntuarioService.cs:28-37`). Como o serviço é `Scoped`, isso roda em todo request de quatro controllers. O mesmo serviço lê e grava arquivos e fabrica bytes falsos quando o arquivo do seed não existe (`434-436`). Uma classe concreta de armazenamento, criada uma vez, e um seed que grava o próprio arquivo de demonstração retiram esse ramo especial da regra de domínio (`02`, seção 6).

**7. Configuração duplicada e divergente.** Há 14 linhas que leem configuração, em quatro arquivos. A chave JWT é lida em `Program.cs:37` para validar e em `JwtTokenGenerator.cs:25` para assinar, com chaves de ambiente e textos padrão diferentes. `Jwt:ExpirationHours` (`appsettings.json:19`) nunca é lida. Dois dos oito nomes de `.env.example` não são lidos por ninguém, e outros dois nunca vencem as chaves do `appsettings.json`. Uma classe de opções por assunto (`JwtOptions`, `StorageOptions`, `MongoOptions`), lida uma vez e usada pelos dois lados, remove a divergência. A convenção nativa `Jwt__SecretKey` substitui os nomes alternativos (`03`, seção 2).

**8. N+1 e consultas sequenciais.** A fila do enfermeiro faz uma ida ao MongoDB por coleta (`TriagemChatService.cs:384-400`). A lista de usuários do admin faz até uma consulta por usuário (`AuxiliaryServices.cs:382-408`), e é a terceira cópia do bloco de `AuthService.cs:41-66` e `80-103`. O dashboard faz até sete consultas em série, quatro delas iguais mudando só o status (`AuxiliaryServices.cs:152-161`). Uma consulta `$in`, uma consulta com junções à esquerda e um `GroupBy` resolvem os três casos (`02`, seções 7 e 8).

**9. Escritas em várias etapas sem fronteira explícita.** Coleta, finalização e validação gravam no PostgreSQL e depois no MongoDB, com a ordem espalhada por três métodos. O comentário de `TriagemChatService.cs:57` diz "idempotente" sobre um `InsertOneAsync` que não é. O cadastro de paciente e o de usuário fazem três gravações independentes cada um, e o upload grava o arquivo antes do registro. A refatoração concentra a gravação da conversa num método e envolve Identity e EF numa única transação. Os efeitos atuais em caso de falha estão na lista de bugs (`02`, seção 9).

**10. Arquivos-gaveta e abstrações que não pagam o custo.** `DomainControllers.cs` reúne 11 controllers de oito áreas. `AuxiliaryServices.cs` reúne cinco serviços sem relação, e `Entities.cs` reúne 13 entidades. A classe base dos controllers (`DomainControllers.cs:11-13`) não é `abstract` e carrega `[ApiController]` e `[Route("api")]`, então é registrada como controller. Os 18 papéis são strings soltas, e os 10 serviços têm interfaces de implementação única sem nenhum teste que as use. A divisão por módulo deve vir depois dos itens 1 a 5, quando os arquivos já tiverem encolhido (`01`, seção 4; `02`, seção 10; `03`, seção 4).

**11. Seed de 422 linhas em um método.** Nove usuários repetem o mesmo padrão de três passos e descartam o `IdentityResult` das 18 chamadas. Os horários são gerados por quatro literais quase iguais. Os comentários de horário estão errados (`DbInitializer.cs:385` e `408`), e a guarda de idempotência deixa um seed parcial sem conserto (`28-31`). Uma tabela de usuários e um produto cartesiano para os horários geram os mesmos dados em cerca de 150 linhas (`03`, seção 6).

**12. Frontend: repetição, tipos frouxos e sintaxe antiga.** Cada assinatura trata carregamento e erro por conta própria: 11 terminam em `alert`, 10 engolem o erro e 13 não têm tratamento nenhum. As regras por perfil (rota inicial, links, cor do selo) estão em quatro lugares, e não há nenhuma guarda de rota. `getStatusBadge` existe três vezes. Há 11 `any`, inclusive onde o tipo já existe (a lista de usuários do admin tem a mesma forma de `UsuarioInfo`). O endereço `http://localhost:5000` está fixo em dois serviços, e o README manda usar um `proxy.conf.json` que não existe. A sintaxe de template é a anterior ao Angular 17 (96 diretivas estruturais). Há código morto, como o `app.component.html` de 336 linhas gerado pelo CLI e quatro injeções sem uso. Detalhes e esboços (`mensagemDe` com `finalize`, tabela `PERFIS` com `perfilGuard`, mapas de selo tipados) em `04_frontend.md`.

**13. Higiene do repositório e documentação desatualizada.** Em `main`, 141 artefatos de build (37,0 MB, com caminhos `D:\mba\...` gravados) estavam versionados, sem `.gitignore` no backend. O commit `415c865` já corrigiu isso, mas falta um `.gitignore` na raiz para o `.env`. Os arquivos `.ai/*.md` e o README descrevem .NET 6 ou 7, Angular 16, uma rota `/api/enfermeiro/validar` e um campo `Disponivel` que não existem. Como esses arquivos são o contexto lido por agentes de IA, a divergência leva o agente a escrever código para outro sistema (`05`, seções 1 a 3).

## Obsolescência e modernização, em síntese

No backend, os recursos do .NET 8 e do C# 12 que substituem código manual são estes: `IExceptionHandler` no lugar dos 27 `catch`; opções tipadas no lugar de `IConfiguration[...] ??`; projeções LINQ no lugar de `foreach` com `List.Add` e de mapeamento manual; `FromSql` interpolado no lugar de `FromSqlRaw` com `{0}` (`AgendaService.cs:121`); `TimeProvider` no lugar de 36 leituras de `DateTime.UtcNow`; e health checks nativos no lugar de um controller que conta a coleção inteira para testar a conexão (`HealthController.cs:41`). Também entram aqui `MapInboundClaims = false` no lugar de limpar o mapa do handler antigo (`Program.cs:35`, a confirmar em execução) e `record` nos 12 DTOs de entrada, com `init` e valor padrão para não trocar `""` por `null`. No frontend, a lista é: controle de fluxo nativo (`@if`, `@for`, `@switch`) com o schematic oficial; `computed` sobre `signal` para estado derivado; `inject()` também nos serviços; remoção de `standalone: true`, que já é o padrão do Angular 19; `HttpParams` no lugar de query strings montadas à mão; e `DATE_PIPE_DEFAULT_OPTIONS` para o formato de data repetido 10 vezes.

## Sequência de remediação proposta

A ordem segue duas regras: primeiro o que reduz risco para os passos seguintes, depois o que mais encolhe o código. As mudanças de lugar (divisão de arquivos) vêm só depois que o conteúdo encolher, para que o diff de movimentação seja pequeno.

1. **Rede de segurança.** Criar um roteiro de caracterização que faz login com as contas do seed, chama as 36 rotas e grava status e corpo; rodar contra `main` para gerar a referência. Ligar um nível moderado de analisadores. Risco nulo, e é o que permite provar "sem mudança de comportamento" em cada passo seguinte (`05`, seção 4).
2. **Limpeza sem risco.** Adicionar `.gitignore` na raiz e remover `app.component.html`/`.css`, os pacotes `BCrypt.Net-Next` e `Microsoft.AspNetCore.OpenApi`, a injeção sem uso em `ColetasController` e as quatro injeções sem uso no frontend. Corrigir também os comentários errados do seed.
3. **Tratador único de erros e `ConflitoException`.** É o item de maior valor por linha alterada, e todos os passos seguintes mexem nos controllers. A mudança visível (nove rotas deixam de responder 500) deve ser registrada como correção deliberada (`01`, seção 2).
4. **Opções tipadas e `.env.example` reescrito.** Elimina a divergência de JWT antes de mexer em identidade (`03`, seção 2).
5. **`UsuarioAtual`.** Remove as 17 consultas e os 11 pares de parâmetros. Verificar com o roteiro que nenhum perfil perdeu ou ganhou acesso (`01`, seção 3).
6. **Projeções de DTO, consulta única de `UsuarioInfoDto` e correção dos N+1** (`02`, seções 4, 7 e 8).
7. **Roteiro da triagem como dado**, fábricas de mensagem e remoção dos campos mortos do MongoDB com `[BsonIgnoreExtraElements]` (`02`, seções 2 e 3).
8. **Regra de acesso clínico única e armazenamento de arquivos fora do serviço** (`02`, seções 5 e 6).
9. **Divisão por módulo.** Um arquivo por controller e por serviço, base `abstract`, constantes de papel e remoção opcional das interfaces de implementação única (`01`, seção 4; `02`, seção 10).
10. **Seed como tabela**, com verificação do `IdentityResult` (`03`, seção 6), e limpeza do `OnModelCreating`, provada por uma migration vazia (`03`, seção 4).
11. **Frontend.** Na ordem: `mensagemDe` com `finalize`, tabela de perfis com guardas, mapas de selo, tipos no lugar de `any`, URL relativa com `proxy.conf.json`, `HttpParams`, schematic de controle de fluxo, CSS comum no `styles.css` e remoção do código morto (`04`).
12. **Documentação.** Reescrever `.ai/*.md` e o README para descrever o sistema que existe (`05`, seção 3).

## Bugs observados (não corrigir nesta refatoração)

Estes defeitos foram encontrados durante a leitura. Ficam registrados aqui para não se misturarem com a refatoração; corrigi-los muda comportamento e merece um trabalho próprio.

- Assinatura e validação do JWT leem fontes diferentes. Com a variável `JWT_SECRET_KEY` definida, todo request autenticado recebe 401 (`Program.cs:37-38`, `JwtTokenGenerator.cs:25-27`).
- A senha do PostgreSQL em `appsettings.json:11` difere da senha de `docker-compose.yml:11`. O erro do seed é engolido (`Program.cs:149-152`) e a API sobe sem banco.
- O README manda testar `http://localhost:5000/health` (`README.md:30-35`), mas a rota é `/api/health`, a porta padrão é 5240 e o `proxy.conf.json` citado não existe.
- Nove ações devolvem 500 para erros esperados (`01`, seção 2). Uma corrida na reserva do mesmo horário devolve 500 em vez de 409 (`AgendaService.cs:189` com o índice de `AppDbContext.cs:51-54`).
- `FinalizarEnvioEnfermeiroAsync` não verifica status nem etapa (`TriagemChatService.cs:335-355`). O paciente pode enviar a coleta na primeira etapa ou devolver à fila uma coleta já validada.
- As transições de consulta não são validadas. É possível concluir consulta cancelada (`ProntuarioService.cs:120` só testa `Concluida`) e iniciar atendimento de consulta cancelada ou concluída (`AgendaService.cs:348-356`). Atendente ou médico de qualquer instituição pode cancelar (`AgendaService.cs:336`).
- `ObterConsultaPorIdAsync` libera qualquer médico e qualquer atendente ou enfermeiro, de qualquer instituição (`AgendaService.cs:291-301`); o comentário promete uma verificação de compartilhamento que não existe. `GET /api/coletas/{id}` tem a mesma abertura para perfis que não são paciente (`TriagemChatService.cs:98-102`).
- O enfermeiro valida coleta de outra instituição (`TriagemChatService.cs:405-414`), e a especificação limita à sua.
- Há filtros ignorados: `medicoId` em `AgendaService.cs:213` e `status` em `DomainControllers.cs:220`. O admin sem instituição recebe a fila vazia (`DomainControllers.cs:228`).
- No upload, o médico anexa documento a qualquer paciente, e a instituição de origem não é validada (uma chave estrangeira inválida vira 500). O arquivo é gravado antes do registro (`ProntuarioService.cs:312-363`), e só a extensão é conferida.
- Na triagem, `Contains("Sim")` também aceita "assim" (`TriagemChatService.cs:199` e `244`), e `Conteudo` nulo causa `NullReferenceException` (`143`). O frontend gera um id de idempotência novo a cada envio (`frontend/src/app/services/api.service.ts:199`), e `EtapaEsperada` nunca é conferida.
- `IniciarColetaAsync` não é idempotente, embora o comentário diga que é (`TriagemChatService.cs:57` e `84`). Se o MongoDB falhar, fica uma coleta sem conversa, que responde 404 (`108`).
- `CriarUsuarioAsync` valida a instituição do médico depois de criar o usuário (`AuxiliaryServices.cs:427` e `441`). Um paciente criado pelo admin não ganha registro `Paciente` (`436`).
- `RegistrarEvolucaoAsync` sobrescreve `CriadaEmUtc` ao editar (`ProntuarioService.cs:64`) e aceita consulta cancelada.
- O histórico do paciente devolve `TemPesquisaRespondida = false` em todas as consultas, porque não carrega `PesquisaSatisfacao` (`ProntuarioService.cs:171-179`).
- No frontend, o link "Pacientes" do atendente chama uma rota exclusiva do admin e mostra uma tabela vazia (`app.routes.ts:39`). A agenda reaproveitada para médico e atendente tem um botão de reserva que não faz nada (`agenda-paciente.component.ts:213`). "Continuar Atendimento" limpa da tela o rascunho salvo (`consultas-medico.component.ts:181`). A expiração do token não é tratada (`auth.service.ts:56-58`).
- O chat renderiza o texto do paciente com `[innerHTML]` (`chat-triagem.component.ts:49`), e a especificação pede exibição com escape.
- O seed pode ficar pela metade e nunca mais se completar (`DbInitializer.cs:28-31` e `55`).

## Arquivos de detalhe

- `01_backend_controllers_e_erros.md`: política de erros, identidade do solicitante, classe base, papéis, health check.
- `02_backend_services.md`: roteiro da triagem, documento MongoDB, projeções de DTO, regra de acesso clínico, infraestrutura nos serviços, N+1, escritas em várias etapas, modernização pontual.
- `03_backend_config_dados_seed.md`: configuração e opções, `Program.cs`, modelo relacional, MongoDB, seed, projeto e perfis de execução.
- `04_frontend.md`: carregamento e erro, perfis e guardas, selos, tipos, URL e parâmetros, sintaxe do Angular 19, código morto, duplicação com o backend, CSS.
- `05_higiene_repositorio_e_docs.md`: artefatos versionados, restos de template, divergência entre documentação e código, rede de segurança para refatorar, segredos.
