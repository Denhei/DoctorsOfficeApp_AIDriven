# 03. A suíte de caracterização (detalhe)

Este arquivo responde à pergunta 5 do enunciado. As perguntas são: o pin testa comportamento ou implementação, quais asserções são frágeis, o que ele não cobre, e se o cenário longo deve ser dividido. A conclusão curta é que a suíte é boa no que testa e passou no teste retroativo. Ela tem, porém, uma lacuna exatamente no trecho que a refatoração mais reescreveu (a regra de consentimento) e algumas asserções que dependem do estado global do banco.

## 1. Estrutura e números

O projeto `Consultorio.Api.Tests` usa xUnit 2.9.2, `Microsoft.AspNetCore.Mvc.Testing` 8.0.11 e Testcontainers 4.1.0 (`Consultorio.Api.Tests.csproj:12-17`). A `ApiFixture` sobe `postgres:16-alpine` e `mongo:7.0` em paralelo, cria a API com `WebApplicationFactory<Program>`, roda migrations e seed, e só libera os testes depois que `/api/health` responde `Healthy` (`ApiFixture.cs:29-56`). Todas as classes compartilham uma coleção e um banco, com paralelismo desligado (`ApiFixture.cs:11-20`).

| Arquivo | Linhas | Testes |
| --- | ---: | ---: |
| `ApiFixture.cs` | 122 | (infraestrutura) |
| `Caracterizacao/AuthTests.cs` | 79 | 6 |
| `Caracterizacao/CatalogoTests.cs` | 115 | 6 |
| `Caracterizacao/PacientesTests.cs` | 82 | 4 |
| `Caracterizacao/AdminTests.cs` | 113 | 4 |
| `Caracterizacao/ProntuarioTests.cs` | 248 | 10 |
| `Caracterizacao/TratamentoDeErrosTests.cs` | 80 | 5 (4 + `CorsEmErrosTests`) |
| `Caracterizacao/TriagemAgendamentoTests.cs` | 467 | 2 |
| `Unidade/RoteiroTriagemTests.cs` | 69 | 14 (1 `Theory` com 11 casos + 3 `Fact`) |
| **Total** | **1.375** | **51** (37 HTTP + 14 de unidade) |

Execução no repositório, com o ambiente pedido no enunciado:

```
$ time dotnet test Consultorio.Api.Tests 2>&1 | tail -8
Aprovado!  – Com falha:     0, Aprovado:    51, Ignorado:     0, Total:    51, Duração: 4 s - Consultorio.Api.Tests.dll (net8.0)
dotnet test Consultorio.Api.Tests 2>&1  9.98s user 1.17s system 58% cpu 18.916 total
```

## 2. O pin testa comportamento, não implementação

Os 37 testes HTTP falam com a API como o frontend fala. Eles fazem login com as contas do seed e verificam status, `Content-Type` quando relevante, campos do JSON e o texto exato de `mensagem`. Os testes da sessão de prontuário são um bom exemplo: `ProntuarioTests.cs:53-71` verifica, perfil por perfil, o status e a mensagem de negação. Isso congela o contrato que o frontend lê, sem depender de como o serviço chega lá. A prova prática é o pin retroativo (seção 3): a mesma suíte passa contra uma implementação estruturalmente muito diferente, a original.

Os bugs congelados de propósito estão comentados no próprio teste, o que é boa prática:

- `temPesquisaRespondida = false` no histórico (`ProntuarioTests.cs:30-32`);
- o Enfermeiro vê histórico vazio (`ProntuarioTests.cs:76-77`);
- a fila do Admin sem instituição vem vazia (`TriagemAgendamentoTests.cs:138-140`).

Há poucos acoplamentos com implementação, todos nos testes de unidade ou num detalhe de framework:

- `RoteiroTriagemTests.cs:67` exige `ArgumentOutOfRangeException` para `Pergunta(1, ...)`. A etapa 1 nunca pede pergunta pelo HTTP (a boas-vindas vem de `RoteiroTriagem.BoasVindas`), então esse teste congela um detalhe interno. Vale manter só se a exceção for parte do contrato do módulo.
- `RoteiroTriagemTests.cs:61-62` compara `"Bot"`, `"Opcoes"` e `"Texto"` como strings. Se as mensagens do Mongo virarem enums (`01`, seção 6), esse teste muda; os testes HTTP continuam protegendo o JSON.
- O nome `Pergunta_existe_uma_vez_por_etapa_e_tem_opcoes_so_nas_etapas_de_escolha` (`RoteiroTriagemTests.cs:53`) promete unicidade, mas o corpo verifica tipo e opções. A unicidade é garantida pela forma do `switch`, não pelo teste.
- `TriagemAgendamentoTests.cs:259` exige `application/problem+json` no 400 de transição inválida. É um artefato do `BadRequest()` vazio com `[ApiController]`, mas o cliente o vê, então é contrato legítimo. A consequência está em `01`, seção 5: esse retorno `bool` não pode virar exceção sem mudar o corpo.

## 3. Pin retroativo

Os testes de `Caracterizacao/` de `HEAD`, rodados contra o código original (`43eef9b`), dão 33/37. As 4 falhas são exatamente os testes de `TratamentoDeErrosTests` que documentam a mudança de 500 para 403/400 (comandos e saída em `02`, seção 4). Os testes acrescentados durante a refatoração passam contra o código original:

- `Enfermeiro_ve_historico_sem_consultas_nem_documentos_mas_com_autorizacoes`, de `a8476ae`;
- `Resposta_de_erro_de_negocio_mantem_cabecalho_cors_para_o_frontend`, de `03f54ed`;
- `Roteiro_com_respostas_negativas_pula_as_etapas_de_detalhe` e a nova sequência de `TriagemCompleta`, de `8d44753`.

Eles descrevem o comportamento antigo, não foram moldados ao código novo. Recomendo registrar esse procedimento no ADR 006 e repeti-lo a cada teste novo.

## 4. Mutantes: o que o pin não detecta

Criei uma cópia de `HEAD` sem as sondas, apliquei um mutante de cada vez e rodei a suíte inteira:

```sh
# Mutante 1: consentimento sempre falso
perl -0pi -e 's/=> Autorizacoes\.Any\(a => a\.InstituicaoOrigemId == instituicaoOrigemId\s*&& a\.InstituicaoDestinoId == instituicaoDestinoId\s*&& a\.RevogadaEmUtc == null\);/=> false;/s' \
  $S/mut/backend/Consultorio.Api/Models/Entities/Entities.cs
dotnet test Consultorio.Api.Tests
# Mutante 2 (com o arquivo restaurado): o médico vê qualquer instituição de origem
sed -i '' 's/podeVerOrigem = origem => origem == medico.InstituicaoId || paciente.TemAutorizacaoAtiva(origem, medico.InstituicaoId);/podeVerOrigem = _ => true;/' \
  $S/mut/backend/Consultorio.Api/Services/ProntuarioService.cs
dotnet test Consultorio.Api.Tests
```

```
Mutante 1 (Entities.cs:81-82 => false):        Aprovado!  – Com falha: 0, Aprovado: 51, Total: 51
Mutante 2 (ProntuarioService.cs:146 _ => true): Aprovado!  – Com falha: 0, Aprovado: 51, Total: 51
```

Os dois mutantes sobrevivem. O primeiro nega todo compartilhamento entre instituições; o segundo libera todo dado clínico para qualquer médico com vínculo. A causa é que nenhum teste cria uma consulta entre um médico e um paciente de outra instituição:

- `Medico_responsavel_ve_historico_da_propria_instituicao` (`ProntuarioTests.cs:93-104`) só exercita o ramo `origem == medico.InstituicaoId`.
- `Medico_de_outra_instituicao_baixa_documento_apenas_com_autorizacao_e_consulta` (`ProntuarioTests.cs:169-178`) é negado antes, em "sem consulta vinculada", e nunca chega à checagem de autorização.

Essa regra foi reescrita duas vezes na branch: o predicado virou `Paciente.TemAutorizacaoAtiva` (`Entities.cs:80-84`), e a consulta SQL do download virou checagem em memória (`ProntuarioService.cs:233` e `250-254`, contra `main:Services/ProntuarioService.cs:413-422`). A sonda P5 de `02` mostra que o comportamento foi preservado, mas o pin sozinho não teria percebido uma regressão. Por isso marquei este item como bloqueante.

## 5. Cobertura

### 5.1 Rotas: 36 de 36

Cada rota da API aparece em pelo menos um teste. Verifiquei com `grep -rF` de um trecho de cada rota nos arquivos de `Caracterizacao/`. A menor contagem foi 1, por exemplo em `/api/instituicoes` e em `/status`. As lacunas estão nos ramos, não nas rotas.

### 5.2 Ramos sem pin

Todos os itens abaixo foram exercitados pelas sondas de `02`, com comportamento idêntico entre base e `HEAD` (salvo onde indicado), e podem virar testes quase sem adaptação.

| Ramo | Código | Sonda |
| --- | --- | --- |
| Consentimento ativo: médico de outra instituição vê consulta, evolução e documento autorizados | `ProntuarioService.cs:146`, `250-254`; `Entities.cs:81-84` | P5 |
| Revogação tira o acesso no histórico e no download | idem | P5 |
| Médico baixa documento da própria instituição | `ProntuarioService.cs:243-255` | P5 |
| Enfermeiro baixa qualquer documento (brecha herdada; congelar com comentário ou levar à lista de bugs) | `ProntuarioService.cs:225-262`, sem ramo para Enfermeiro | P5 |
| Médico sem registro de `Medico` (403 em histórico, download, evolução, conclusão e consulta por id) | `ProntuarioService.cs:31`, `66` e `355-357`; `AgendaService.cs:220-223` | P1 |
| Paciente sem registro de `Paciente` (401 ao iniciar coleta; 403 em notificações e pesquisa; lista vazia de consultas) | `DomainControllers.cs:98-101`; `AuxiliaryServices.cs:84-85`; `AgendaService.cs:175` | P2 (2 rotas mudaram de 500 para 403) |
| Paciente inexistente em histórico, documentos e autorizações (400) | `ProntuarioService.cs:124`, `325` | Sondagem2 (as 3 mudaram de 500 para 400) |
| Validação reprovada (status `EncaminhadaPresencial` e mensagem de orientação) | `TriagemChatService.cs:171` e `178-180` | P6 |
| Finalizar coleta alheia (403) e inexistente (400); mensagem para coleta inexistente (400) | `TriagemChatService.cs:187-201` | P7 |
| Enfermeiro sem instituição na fila (403 sem corpo) | `DomainControllers.cs:110-113` | P8 |
| Upload acima de 5 MB (400) | `ProntuarioService.cs:184-187` | P9 |
| Instituição inexistente no upload e na concessão (500) | `ProntuarioService.cs:204`, `287` e `289` | P4 (o corpo do 500 mudou) |
| Leitura de uma conversa gravada pelo seed (documento Mongo antigo) | `DbInitializer.cs:131-158`; `ConversaMongo.cs:6` | nenhuma |

O último item merece atenção. `[BsonIgnoreExtraElements]` (`ConversaMongo.cs:6`) foi acrescentado justamente para ler documentos antigos que ainda têm `Status` e `VersaoRoteiro`. Nenhum teste lê uma conversa que não tenha sido criada pelo próprio teste. O seed atual também não grava mais esses campos, então nem um teste novo com o seed atual provaria a compatibilidade. Para provar, o teste precisa inserir no Mongo um documento no formato antigo (com `Status` e `VersaoRoteiro`) e chamar `GET /api/coletas/{id}`.

## 6. Fragilidades

**Contagens exatas dependem do estado global.** O dashboard espera 0 agendadas, 2 concluídas, 2 pesquisas e média 4,5 na instituição A, além de 1 cancelada na B (`TriagemAgendamentoTests.cs:364-383`). Esses números só valem porque nenhum outro teste cria consulta nem coleta pendente nessas instituições. A ordem das classes não é controlada (`ApiFixture.cs:11-20` só desliga o paralelismo). Hoje a condição vale. Um teste futuro que agende uma consulta em Santa Maria quebra o dashboard dependendo da ordem de execução. O remédio é ler o dashboard antes e depois das ações do cenário e verificar a diferença.

**Uma asserção pode passar sem verificar nada.** `Slots_filtram_por_data` (`CatalogoTests.cs:71-80`) usa `Assert.All` sobre os slots de amanhã em São Bento. O seed só cria slots em dias úteis (`DbInitializer.cs:249`), então numa sexta ou num sábado a lista vem vazia e o teste passa sem verificar nada. O remédio é escolher o próximo dia útil e acrescentar `Assert.NotEmpty(slots)`.

**Pacientes do seed são compartilhados e alterados.** `PacientesTests.cs:63` muda o telefone da Mariana de forma permanente. `ProntuarioTests.cs:181-216` cria e revoga autorizações dela, e o cenário consome slots e cria consultas para ela. Nenhuma asserção atual depende desses efeitos; por exemplo, a boas-vindas verifica a data de nascimento, não o telefone (`TriagemAgendamentoTests.cs:47-48`). Mesmo assim, o acoplamento existe. Testes que alteram estado deveriam criar o próprio paciente via `POST /api/pacientes`, como `PacientesTests.cs:25-49` já faz.

**Há acoplamento implícito à forma do seed.** `TriagemAgendamentoTests.cs:212` exige exatamente um slot da Dra. Ana Paula no mesmo horário do slot escolhido do Dr. Carlos. Isso vale porque o seed gera a mesma grade para os dois médicos (`DbInitializer.cs:250-252`). É aceitável, mas merece um comentário no teste.

## 7. O cenário de 366 linhas

`Fluxo_completo_da_triagem_ao_dashboard` ocupa `TriagemAgendamentoTests.cs:18-384`: um único `[Fact]` com 14 fases numeradas, de coleta, roteiro e fila até agendamento, atendimento, notificações, pesquisa e dashboard. O comentário da classe justifica a escolha: "Um único teste porque cada passo depende do estado deixado pelo anterior" (`TriagemAgendamentoTests.cs:11`).

Como teste de fumaça de ponta a ponta, o formato é aceitável. Como pin principal de oito áreas, tem três custos:

- a primeira falha esconde todas as fases seguintes;
- o relatório de falha só dá a linha, não a fase;
- as fases compartilham variáveis (`consultaId` nasce na fase 7 e é usado até a 14) e a paciente Mariana com o resto da suíte.

Dividir em vários `[Fact]` dependentes uns dos outros custaria a garantia de ordem. No xUnit 2, a ordem dos testes de uma classe é determinística, mas não segue a ordem do arquivo, a menos que se configure um `ITestCaseOrderer` próprio. A divisão certa é outra: cada teste monta as próprias pré-condições com fluxos auxiliares, a partir de um paciente novo. Assim, nenhuma ordem é necessária.

```csharp
// Fluxos auxiliares (num arquivo Cenarios.cs compartilhado)
async Task<(HttpClient cliente, Guid pacienteId)> NovoPacienteAsync()            // atendente faz POST /api/pacientes; login
async Task<Guid> TriagemFinalizadaAsync(HttpClient paciente, Guid instituicao)    // 7 mensagens e finalização
async Task<Guid> TriagemValidadaAsync(HttpClient paciente, Guid instituicao)      // a anterior mais a validação do enfermeiro
async Task<Guid> ConsultaAgendadaAsync(HttpClient cliente, Guid pacienteId, Guid coleta, Guid medico)
async Task<Guid> ConsultaConcluidaAsync(Guid consulta)                           // EmAtendimento, evolução, conclusão

[Fact] public async Task Roteiro_completo_com_ramos_sim_e_nao_sei()          { var (p, _) = await NovoPacienteAsync(); ... }
[Fact] public async Task Fila_do_enfermeiro_e_por_instituicao()             { ... await TriagemFinalizadaAsync(...); ... }
[Fact] public async Task Agendamento_valida_coleta_instituicao_e_horario()  { ... }
[Fact] public async Task Conflitos_de_slot_e_de_horario_do_paciente_sao_409() { ... }
[Fact] public async Task Visibilidade_das_consultas_por_perfil()             { ... }
[Fact] public async Task Atendimento_evolucao_e_conclusao()                  { ... }
[Fact] public async Task Conclusao_gera_dois_convites_uma_unica_vez()        { ... }
[Fact] public async Task Pesquisa_de_satisfacao()                            { ... }
[Fact] public async Task Dashboard_reflete_as_mudancas()                     { var antes = ...; /* ações */; var depois = ...; /* diferenças */ }
```

Os custos são administráveis:

- **Tempo.** Cada teste refaz a triagem, umas 10 chamadas HTTP. A suíte inteira hoje leva 4 s de execução de teste, então mesmo dez vezes mais seria aceitável.
- **Slots.** O seed cria 60 slots futuros por médico (5 dias úteis × 6 horas × 2 meias-horas, `DbInitializer.cs:245-260`), suficientes para uma dúzia de testes que agendam.
- **Dashboard.** As asserções passam a ser por diferença, o que também resolve a primeira fragilidade da seção 6.

Vale manter uma versão enxuta do cenário atual como fumaça, com uma asserção por fase.

## 8. Testes a acrescentar antes do próximo passo de refatoração

Em ordem de valor, para rodar contra `43eef9b` e contra `HEAD`, como na seção 3:

1. Consentimento entre instituições. O médico da instituição B ganha uma consulta com Carlos, vê a consulta, a evolução e o documento de A, e baixa o documento. Depois que Carlos revoga, ele vê só B e recebe 403 "Acesso negado: Autorização de compartilhamento ausente ou revogada." no download. Isso mata os dois mutantes da seção 4.
2. As 5 rotas que saíram de 500 sem pin (seção 5.2 e `02`, seção 2.3).
3. Usuários com perfil sem registro (sondas P1 e P2), que são o caminho onde `UsuarioAtual` substituiu buscas no banco.
4. Validação reprovada, upload acima de 5 MB e fila de enfermeiro sem instituição.
5. Leitura de um documento Mongo no formato antigo, antes de qualquer mudança de tipo em `MensagemMongo`.
6. Uma verificação de higiene, fora do xUnit: `grep -c '^fail:'` no log do `dotnet test`, para que o ruído de log da seção 3 de `02` não volte depois de corrigido.
