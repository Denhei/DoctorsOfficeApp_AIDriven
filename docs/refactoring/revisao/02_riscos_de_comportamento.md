# 02. Riscos de comportamento (detalhe)

Este arquivo responde à pergunta 3 do enunciado: onde a refatoração depende de algo que o pin talvez não veja e se cada dependência é segura. A seção 1 dá o veredito de cada risco. As seções seguintes mostram as execuções que sustentam os vereditos: sondas HTTP comparando o código original com `HEAD`, contagem de logs, sondas de infraestrutura e o pin retroativo. O código das sondas está no apêndice. Nada foi gravado em `backend/` nem em `frontend/`; tudo rodou em cópias exportadas com `git archive` no diretório temporário da sessão.

## 1. Vereditos

| # | Risco | Onde | Veredito | Evidência | Teste mínimo que expõe |
| --- | --- | --- | --- | --- | --- |
| 1 | Fixup de navegação: `ToDto` de consulta e de autorização lê `c.Paciente` e `a.Paciente` sem `Include` explícito | `ProntuarioService.cs:115-123`, `162` e `165`; `Mapeamentos.cs:14` e `61` | Seguro. O EF preenche a navegação inversa das coleções incluídas a partir de `Paciente`. | Pin: `ProntuarioTests.cs:11-50` responde 200. Sonda P5: linha de 2.041 caracteres, com o corpo completo, idêntica entre base e `HEAD`, e `pacienteNome` "Carlos Silva" em cada consulta. | Já coberto: uma navegação nula daria `NullReferenceException` e 500. |
| 2 | Claims no lugar de buscas no banco (`UsuarioAtual`) | `UsuarioAtual.cs:12-27`; consumidores na tabela 1.3 de `01` | Seguro hoje. A divergência exige mudar `Paciente`, `Medico` ou `InstituicaoId` de um usuário depois do login, e nenhuma rota faz isso. `POST /api/pacientes` cria usuário e paciente juntos (`AuxiliaryServices.cs:197-215`); `POST /api/admin/usuarios` cria usuário e médico juntos (`251-271`). O token vale 24 h (`appsettings.json:19`). | Sondas P1 e P2 (usuários com perfil sem registro): iguais à base, exceto as mudanças de 500 para 403 da seção 2. | Transformar P1 e P2 em testes. |
| 3 | `Add` no lugar de `AddAsync` | 24 trocas (`AddAsync\(|AddRangeAsync\(`: 24 para 0) | Seguro. As chaves são `Guid` inicializadas na própria entidade (`Entities.cs:33`, `54`, `90`, `116`, `138`, `171`, `202`, `223`, `257`, `282` e `307`). `AddAsync` só importa para gerador de valor assíncrono. | O pin cria consultas, slots, coletas, documentos, autorizações, pesquisas, evoluções e notificações, todos com 200. | Já coberto. |
| 4 | `Include` removidos | `main:Services/AuxiliaryServices.cs:202-205` (`ListarMedicosAsync`), `main:Services/TriagemChatService.cs:407-409` (`ValidarColetaAsync`), `main:Services/ProntuarioService.cs:443-446` (`ConcederAutorizacaoAsync`), `main:Services/AgendaService.cs:166-171` (conflito do paciente) | Seguro. No primeiro, o `Include` é ignorado numa projeção. No segundo e no terceiro, a navegação não era lida. No quarto, o `Include` é ignorado num `AnyAsync`. | Pin: médicos por instituição (`CatalogoTests.cs:34-50`), validação (`TriagemAgendamentoTests.cs:147-153`), autorizações (`ProntuarioTests.cs:181-216`) e 409 de horário (`TriagemAgendamentoTests.cs:212-215`). | Já coberto. |
| 5 | `FindAsync(solicitante.MedicoId)` com `Guid?` nulo | `ProntuarioService.cs:355-357` | Seguro. Com chave nula, o `FindAsync` devolve `null`, o `?? throw` dispara e a resposta é 403 "Médico não encontrado.", a mesma da base. | Sonda P1: `historico`, `documentos` e `download` dão 403 com essa mensagem nas duas versões. | P1 como teste. |
| 6 | `InstituicaoOrigem = instituicao!`, `origem!` e `destino!` | `ProntuarioService.cs:204`, `287` e `289` | Status preservado (500), porque a chave estrangeira derruba o `SaveChanges` antes do `ToDto`. O `!` mente sobre a nulidade (ver `01`, seção 3.1). O arquivo do upload é gravado antes da falha (`ProntuarioService.cs:195` antes de `214`), um bug herdado. | Sonda P4: 500 nas duas versões; o corpo mudou (seção 2). | P4 como teste, se o 500 dever ser congelado. |
| 7 | `ToDictionary` pelo `_id` do Mongo | `TriagemChatService.cs:143-145` | Seguro. `_id` é único por definição. | Pin: fila do enfermeiro com resumo (`TriagemAgendamentoTests.cs:129-134`). | Já coberto. |
| 8 | Tradução do `GroupBy` | `AuxiliaryServices.cs:109-112` | Seguro. Se não traduzisse, lançaria exceção; as contagens por status são exatas no pin. | `TriagemAgendamentoTests.cs:364-383`. | Já coberto. |
| 9 | Nome do médico na evolução | `Mapeamentos.cs:44-45` lê o autor. Em `main`, o histórico lia o médico do slot (`main:Services/ProntuarioService.cs:269`) e o registro lia o solicitante (`main:...:81-87`). | Seguro por invariante: só o médico do slot registra a evolução (`ProntuarioService.cs:39`), e o seed usa `medA` nos dois papéis (`DbInitializer.cs:163` e `187`). | Pin: `TriagemAgendamentoTests.cs:289` e `ProntuarioTests.cs:36`. | Não alcançável pela API. |
| 10 | `pacienteId` e `medicoId` resolvidos para qualquer perfil | `Usuarios.cs:22-23`; em `main`, só para o perfil correspondente (`main:Services/AuxiliaryServices.cs:387-396`) | Seguro pelos índices únicos (`AppDbContext.cs:34-40`) e pelos caminhos de criação. | Sonda P3: mesma lista, mesma ordem, mesmos marcadores. | P3 como teste de forma, sem ordem. |
| 11 | Navegações atribuídas antes do `Add` | `AgendaService.cs:75-83` e `149-159`; `TriagemChatService.cs:32-41`; `ProntuarioService.cs:52`, `199-211` e `282-292` | Seguro hoje, porque as entidades atribuídas vieram de consultas rastreadas. Frágil a um `AsNoTracking()` futuro (não verificado em execução). | O pin cria todas essas entidades. | A projeção de `01`, seção 3, remove o risco. |
| 12 | Rotas que saíram de 500 além das 4 documentadas | 5 rotas | Mudança visível, coerente com o diagnóstico, mas sem pin e descrita como "4 endpoints" no commit `03f54ed`. | Seção 2. | Sondas da seção 2 como testes. |
| 13 | Corpo das respostas 500 | `Program.cs:79` e `155` | No ambiente Development, a página de exceção em texto puro virou ProblemDetails. O status é o mesmo. | Sonda P4. | Opcional. |
| 14 | Volume de log de erro | `Program.cs:78` e `155` | Regressão operacional: de 4 para 45 entradas `fail` com stack trace por execução. | Seção 3. | Contar `^fail:` no log do `dotnet test`. |

## 2. Sondas HTTP: código original contra `HEAD`

### 2.1 Método

A base é o commit `43eef9b`, o primeiro com testes. O código de produção dele é `main` mais `public partial class Program { }`, como mostra `git diff --stat main 43eef9b -- backend/Consultorio.Api`, que lista só `Program.cs` (3 linhas) e a remoção de artefatos de build. Exportei as duas versões, copiei a mesma classe de sonda para as duas (apêndice A) e rodei só ela, com containers novos a cada execução:

```sh
R=<repositório>; S=<diretório temporário da sessão>
git -C $R archive 43eef9b backend | tar -x -C $S/base
git -C $R archive HEAD    backend | tar -x -C $S/head
cp SondagemTests.cs $S/base/backend/Consultorio.Api.Tests/Sondagem/
cp SondagemTests.cs $S/head/backend/Consultorio.Api.Tests/Sondagem/
cd $S/base/backend && PROBE_OUT=$S/probe_base_full.txt dotnet test Consultorio.Api.Tests --filter "FullyQualifiedName~Sondagem"
cd $S/head/backend && PROBE_OUT=$S/probe_head_full.txt dotnet test Consultorio.Api.Tests --filter "FullyQualifiedName~Sondagem"
diff $S/probe_base_full.txt $S/probe_head_full.txt
```

Cada linha registra rótulo, status, `Content-Type` e corpo completo (até 6.000 caracteres; o maior corpo teve 3.873). GUIDs, datas ISO e `traceId` são normalizados. Assim, o `diff` só acusa diferença de comportamento.

### 2.2 Resultado

São 37 linhas por versão. A comparação dá 33 linhas idênticas, com o corpo inteiro, e 4 diferentes:

```
13c13
< P2 notificacoes | 500 | text/plain | System.UnauthorizedAccessException: Paciente não encontrado.\n   at ...
---
> P2 notificacoes | 403 | application/json | {"mensagem":"Paciente não encontrado."}
15c15
< P2 pesquisa get | 500 | text/plain | System.UnauthorizedAccessException: Paciente não encontrado.\n   at ...
---
> P2 pesquisa get | 403 | application/json | {"mensagem":"Paciente não encontrado."}
18,19c18,19
< P4 anexar instituicao inexistente | 500 | text/plain | Microsoft.EntityFrameworkCore.DbUpdateException: ...
< P4 conceder instituicao inexistente | 500 | text/plain | Microsoft.EntityFrameworkCore.DbUpdateException: ...
---
> P4 anexar instituicao inexistente | 500 | application/problem+json | {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1", ...
> P4 conceder instituicao inexistente | 500 | application/problem+json | {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1", ...
```

Saída de `HEAD`, com os corpos longos cortados em 170 caracteres:

```
P1 criar medico sem instituicao | 400 | application/json | {"mensagem":"Médico deve ter uma instituição vinculada."}
P1 me | 200 | application/json | {"id":"<guid>","nome":"Dr Orfao",...,"perfil":"Medico","instituicaoId":null,...,"medicoId":null}
P1 historico Carlos | 403 | application/json | {"mensagem":"Médico não encontrado."}
P1 documentos Carlos | 403 | application/json | {"mensagem":"Médico não encontrado."}
P1 download doc Carlos | 403 | application/json | {"mensagem":"Médico não encontrado."}
P1 listar consultas | 200 | application/json | []
P1 consulta por id | 403 | application/json | {"mensagem":"Acesso negado."}
P1 evolucao | 403 | application/json | {"mensagem":"Usuário não é um médico cadastrado."}
P1 conclusao | 403 | application/json | {"mensagem":"Usuário não é um médico."}
P1 iniciar atendimento | 403 | application/json | {"mensagem":"Apenas o médico responsável pode iniciar o atendimento."}
P2 criar paciente via admin | 200 | application/json | {...,"perfil":"Paciente",...,"pacienteId":null,"medicoId":null}
P2 iniciar coleta | 401 | application/problem+json | {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.2","title":"Unauthorized","status":401,...}
P2 notificacoes | 403 | application/json | {"mensagem":"Paciente não encontrado."}
P2 consultas | 200 | application/json | []
P2 pesquisa get | 403 | application/json | {"mensagem":"Paciente não encontrado."}
P2 pesquisa put | 403 | application/json | {"mensagem":"Paciente não encontrado."}
P3 usuarios | atendente.santamaria:--,enfermeiro.santamaria:--,medico.santamaria:-m,atendente.saobento:--,enfermeiro.saobento:--,
             medico.saobento:-m,orfao.paciente:--,orfao.medico:--,paciente.mariana:p-,paciente.carlos:p-,admin:--
P4 anexar instituicao inexistente | 500 | application/problem+json | {"type":...,"title":"An error occurred while processing your request.","status":500}
P4 conceder instituicao inexistente | 500 | application/problem+json | {...,"status":500}
c5- validar aprovado=True | 200 | application/json | {"mensagem":"Triagem validada com sucesso!"}
P5 agendar Carlos com medB | 200 | application/json | {...,"pacienteNome":"Carlos Silva",...,"medicoNome":"Dra. Ana Paula",...}
P5 historico medB com autorizacao | 200 | application/json | {...}
P5 download medB com autorizacao | 200 | application/pdf | [Arquivo Fictício de Demonstração]: Exame_Eletrocardiograma_Ficticio.pdf ...
P5 download medA propria instituicao | 200 | application/pdf | [Arquivo Fictício de Demonstração]: ...
P5 revogar autorizacao seed | 200 | application/json | {"mensagem":"Autorização revogada com sucesso."}
P5 historico medB sem autorizacao | 200 | application/json | {...}
P5 download medB sem autorizacao | 403 | application/json | {"mensagem":"Acesso negado: Autorização de compartilhamento ausente ou revogada."}
P5 enfermeiro baixa doc | 200 | application/pdf | [Arquivo Fictício de Demonstração]: ...
c6- validar aprovado=False | 200 | application/json | {"mensagem":"Triagem validada com sucesso!"}
P6 status e ultima mensagem | EncaminhadaPresencial / ⚠️ **Orientação da Enfermagem:** ...
P7 finalizar coleta de outro | 403 | application/json | {"mensagem":"Acesso negado."}
P7 finalizar coleta inexistente | 400 | application/json | {"mensagem":"Coleta não encontrada."}
P7 mensagem coleta inexistente | 400 | application/json | {"mensagem":"Coleta não encontrada."}
P7 medico le coleta | 200 | application/json | {...,"statusColeta":"EncaminhadaPresencial",...}
P8 criar enfermeiro sem instituicao | 200 | application/json | {...,"perfil":"Enfermeiro","instituicaoId":null,...}
P8 fila | 403 |  |
P9 upload > 5MB | 400 | application/json | {"mensagem":"O arquivo excede o limite máximo permitido de 5 MB."}
```

Os dois históricos da sonda P5, decodificados com `python3 -I -c 'json.loads(...)'`, ficaram assim:

```
P5 historico medB com autorizacao 200 consultas: [('Clínica Santa Maria', 'Dr. Carlos Roberto', False), ('Clínica São Bento', 'Dra. Ana Paula', False)]
                                       docs: ['Clínica Santa Maria'] evol: ['Dr. Carlos Roberto'] auts: 1
P5 historico medB sem autorizacao 200 consultas: [('Clínica São Bento', 'Dra. Ana Paula', False)] docs: [] evol: [] auts: 0
```

O que isso prova:

- **P5.** O ramo positivo do consentimento (`TemAutorizacaoAtiva` verdadeiro) e o efeito da revogação estão preservados, no histórico e no download. É exatamente o ramo que o pin não exercita (`03`, seção 4).
- **P1 e P2.** As substituições de busca no banco por claims (`ProntuarioService.cs:31`, `66` e `355-357`; `AgendaService.cs:175`, `180` e `220`; `AuxiliaryServices.cs:84-85`) preservam status e mensagens para usuários com perfil sem registro. A exceção são as duas rotas que antes davam 500.
- **P3.** A projeção única de usuários devolve a mesma lista, na mesma ordem, com os mesmos `pacienteId` e `medicoId`.
- **P4.** O `!` não muda o status; só o corpo do 500 mudou.
- **P6 a P9.** Validação reprovada, coleta alheia ou inexistente, leitura de coleta por médico de outra instituição, fila de enfermeiro sem instituição e upload acima de 5 MB não mudaram. A linha `P5 enfermeiro baixa doc | 200` registra uma brecha de política herdada da base (ver `01`, seção 4.1).

### 2.3 As outras rotas que saíram de 500

Uma segunda sonda (apêndice B) pede histórico, documentos e autorizações de um paciente inexistente:

```
BASE
historico | 500 | text/plain | System.InvalidOperationException: Paciente não encontrado.
documentos | 500 | text/plain | System.InvalidOperationException: Paciente não encontrado.
autorizacoes | 500 | text/plain | System.InvalidOperationException: Paciente não encontrado.
HEAD
historico | 400 | application/json | {"mensagem":"Paciente não encontrado."}
documentos | 400 | application/json | {"mensagem":"Paciente não encontrado."}
autorizacoes | 400 | application/json | {"mensagem":"Paciente não encontrado."}
```

Somando as 2 rotas de P2 e as 3 acima às 4 de `TratamentoDeErrosTests.cs`, são 9 rotas que deixaram de responder 500. É exatamente a tabela do diagnóstico (`docs/refactoring/diagnostico/01_backend_controllers_e_erros.md:41-53`). O commit `03f54ed` e o pin registram 4. A mudança é desejada e foi prevista. O que falta é pinar as outras 5 e corrigir o número na mensagem ou no ADR.

## 3. Volume de log de erro

```sh
dotnet test Consultorio.Api.Tests 2>&1 > head_test.log          # HEAD, no repositório
grep -c '^fail:' head_test.log
grep '^fail:' head_test.log | sort | uniq -c
# base: os 37 testes HTTP de HEAD rodando contra 43eef9b (seção 4)
grep -c '^fail:' base_pin.log
grep '^fail:' base_pin.log | sort | uniq -c
```

```
head fail: 45
  45 fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]
       21 System.InvalidOperationException, 22 System.UnauthorizedAccessException, 2 ConflitoException
base fail: 4
   4 fail: Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware[1]
```

Cada entrada de `HEAD` traz "An unhandled exception has occurred while executing the request." e um stack trace de cerca de 18 linhas. A primeira é, por exemplo, `System.InvalidOperationException: Médico deve ter uma instituição vinculada.`, um 400 esperado. Na base, as 4 entradas são os 4 casos de `TratamentoDeErrosTests` que respondiam 500.

**Experimento.** Numa cópia de `HEAD`, troquei `AddExceptionHandler<TratadorGlobalDeExcecoes>()` por um filtro global do MVC com o mesmo `switch` (código em `01`, seção 2.3) e rodei a suíte inteira:

```
Aprovado!  – Com falha:     0, Aprovado:    51, Ignorado:     0, Total:    51, Duração: 9 s - Consultorio.Api.Tests.dll (net8.0)
fail: 0
```

O contrato HTTP, inclusive o CORS em resposta de erro, fica igual, e o ruído de log desaparece.

## 4. Pin retroativo

Copiei todos os arquivos de `Caracterizacao/` de `HEAD` para a exportação de `43eef9b` e rodei só eles. Os testes de unidade ficaram de fora porque dependem de `RoteiroTriagem`, que não existe na base.

```sh
cp $S/head/backend/Consultorio.Api.Tests/Caracterizacao/*.cs $S/base/backend/Consultorio.Api.Tests/Caracterizacao/
cd $S/base/backend && dotnet test Consultorio.Api.Tests --no-build --filter "FullyQualifiedName~Caracterizacao"
```

```
Com falha Consultorio.Api.Tests.Caracterizacao.TratamentoDeErrosTests.Criar_paciente_com_email_ja_usado_recebe_400
Com falha Consultorio.Api.Tests.Caracterizacao.TratamentoDeErrosTests.Paciente_atualizando_cadastro_de_outro_recebe_403
Com falha Consultorio.Api.Tests.Caracterizacao.TratamentoDeErrosTests.Paciente_lendo_cadastro_de_outro_recebe_403
Com falha Consultorio.Api.Tests.Caracterizacao.TratamentoDeErrosTests.Iniciar_coleta_em_instituicao_inexistente_recebe_400
Com falha! – Com falha:     4, Aprovado:    33, Ignorado:     0, Total:    37, Duração: 7 s - Consultorio.Api.Tests.dll (net8.0)
```

As únicas falhas são as quatro mudanças declaradas. Os testes acrescentados durante a refatoração (enfermeiro no histórico em `a8476ae`, CORS em erro em `03f54ed`, ramos negativos do roteiro e resposta vazia em `8d44753`) passam contra o código original, ou seja, descrevem o comportamento antigo e não o novo. Esse é o resultado mais importante desta seção: os testes não foram ajustados ao código refatorado.

## 5. Sondas de infraestrutura

**Configuração ausente vira 400.** Uma `WebApplicationFactory` própria (apêndice C) aponta o PostgreSQL para uma porta fechada (`Port=1;Timeout=1`) e define `MongoDatabaseName` como nulo:

```
>>> Erro ao inicializar banco de dados: MongoDatabaseName não configurado.        (log da subida, Program.cs:150)
/api/health | 400 | application/json | {"mensagem":"MongoDatabaseName não configurado."}
/api/coletas/00000000-0000-0000-0000-000000000001 | 401 |  |
```

O seed engole a exceção (`Program.cs:148-151`), a API sobe, e cada rota que depende do Mongo passa a responder 400 com uma mensagem de configuração do servidor. Em `main` havia fallbacks embutidos, então esse caminho não existia. A remoção deles é uma mudança documentada; o efeito colateral sobre o status HTTP é o problema.

**Falta de permissão em disco vira 403 com caminho do servidor.** A sonda do apêndice D tira a permissão de escrita do diretório de upload da fixture e faz um upload:

```
HEAD: upload em diretorio somente leitura | 403 | {"mensagem":"Access to the path '/var/folders/6l/.../consultorio-tests-15a9.../7d55e39d-....pdf' is denied."}
BASE: upload em diretorio somente leitura | 403 | {"mensagem":"Access to the path '/var/folders/6l/.../consultorio-tests-6c78.../c69d73c2-....pdf' is denied."}
```

Nesta rota o comportamento é o mesmo da base, porque a ação original já capturava `UnauthorizedAccessException`. A diferença é o alcance: em `HEAD`, a mesma tradução vale para todas as rotas. As duas sondas sustentam o achado 1 do resumo.

## Apêndice A. `SondagemTests.cs` (rodada nas duas versões)

```csharp
[Collection(ApiCollection.Nome)]
public class SondagemTests(ApiFixture api)
{
    private static readonly string Saida = Environment.GetEnvironmentVariable("PROBE_OUT") ?? "/private/tmp/probe.txt";

    private static string Normalizar(string s)
    {
        s = Regex.Replace(s, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "<guid>");
        s = Regex.Replace(s, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z?", "<ts>");
        s = Regex.Replace(s, @"""traceId"":""[^""]*""", "\"traceId\":\"<t>\"");
        s = s.Replace("\n", "\\n");
        return s.Length > 6000 ? s[..6000] + "..." : s;
    }

    private static async Task Log(string rotulo, HttpResponseMessage r)
        => File.AppendAllText(Saida, $"{rotulo} | {(int)r.StatusCode} | {r.Content.Headers.ContentType?.MediaType} | {Normalizar(await r.Content.ReadAsStringAsync())}\n");

    // TriagemValidadaAsync: inicia coleta, responde 6 mensagens, finaliza e valida (aprovar = true/false)

    [Fact]
    public async Task Sondagem_completa()
    {
        var admin = await api.ComoAsync(Seed.Admin);
        // P1: POST /api/admin/usuarios perfil=Medico sem instituição (400, mas o usuário fica criado); login;
        //     GET /api/auth/me, /pacientes/{Carlos}/historico, /pacientes/{Carlos}/documentos, /documentos/{DocCarlos}/arquivo,
        //     GET /api/consultas, /consultas/{ConsultaCarlos}; PUT .../evolucao; POST .../conclusao; PATCH .../status EmAtendimento
        // P2: POST /api/admin/usuarios perfil=Paciente (sem registro Paciente); login;
        //     POST /api/coletas; GET /api/notificacoes; GET /api/consultas; GET e PUT /consultas/{ConsultaCarlos}/pesquisa
        // P3: GET /api/admin/usuarios e registra email:p/m na ordem devolvida
        // P4: Carlos anexa documento e concede autorização com instituição inexistente
        // P5: Carlos faz triagem em São Bento validada por EnfermeiroB, agenda com MedicoB;
        //     MedicoB: histórico e download do documento de Santa Maria; MedicoA: download;
        //     Carlos revoga a autorização do seed; MedicoB: histórico e download de novo; EnfermeiroB: download
        // P6: triagem de Carlos em Santa Maria reprovada por EnfermeiroA; registra status e última mensagem
        // P7: Mariana finaliza a coleta de Carlos e uma inexistente; envia mensagem a coleta inexistente; MedicoB lê a coleta
        // P8: admin cria Enfermeiro sem instituição; GET /api/coletas
        // P9: upload de 5 MB + 1 byte
    }
}
```

O arquivo completo (cerca de 130 linhas) ficou no diretório temporário da sessão e não foi versionado. Os comentários acima listam cada chamada na ordem em que foi feita.

## Apêndice B. `Sondagem2Tests.cs`

Com `Guid.NewGuid()` como paciente, `MedicoA` faz `GET /api/pacientes/{id}/historico` e `GET /api/pacientes/{id}/documentos`, e `Mariana` faz `GET /api/pacientes/{id}/autorizacoes`. A sonda registra status, `Content-Type` e a primeira linha do corpo.

## Apêndice C. `ConfiguracaoTests.cs` (só em `HEAD`)

```csharp
var settings = new Dictionary<string, string?>
{
    ["ConnectionStrings:PostgresConnection"] = "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1",
    ["ConnectionStrings:MongoConnection"] = "mongodb://127.0.0.1:1",
    ["MongoDatabaseName"] = null,
};
await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(settings)));
// GET /api/health e GET /api/coletas/{id}, registrando status, Content-Type e corpo
```

## Apêndice D. `Sondagem3Tests.cs` (rodada nas duas versões)

A sonda lê o campo privado `_uploads` da `ApiFixture` por reflexão e aplica `File.SetUnixFileMode(dir, UserRead | UserExecute)`. Depois, Carlos faz o upload de um PDF para `instituicaoOrigemId = InstA`, e a sonda registra o status e o corpo. A permissão é restaurada num `finally`.
