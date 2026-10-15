# 01. Backend: controllers, política de erros e identidade do solicitante

Linha de base: commit `8ff4b1d` (branch `main`). Caminhos relativos a `backend/Consultorio.Api/`, salvo indicação. Todas as medições abaixo foram feitas sobre uma exportação de `main` (`git archive main | tar -x -C <dir>`), para não sofrer interferência de commits feitos na branch `refactoring` durante a auditoria.

## 1. Medições

| Arquivo | Linhas | Tipos declarados | Ações HTTP |
| --- | ---: | --- | ---: |
| `Controllers/DomainControllers.cs` | 596 | 12 classes (1 base + 11 controllers) | 33 |
| `Controllers/AuthController.cs` | 61 | 1 classe | 2 |
| `Controllers/HealthController.cs` | 62 | 1 classe | 1 |

Comandos (executados em `backend/Consultorio.Api/`):

```bash
wc -l Controllers/*.cs
grep -cE '^public (static )?class ' Controllers/DomainControllers.cs          # 12
grep -cE '^\s*\[Http(Get|Post|Put|Patch|Delete)' Controllers/DomainControllers.cs   # 33
grep -c  '^\s*try\s*$' Controllers/DomainControllers.cs                         # 18
grep -c  'catch (UnauthorizedAccessException' Controllers/DomainControllers.cs   # 16
grep -c  'catch (InvalidOperationException' Controllers/DomainControllers.cs     # 10
grep -c  'catch (Exception' Controllers/DomainControllers.cs                     # 1
grep -c  'StatusCode(StatusCodes.Status403Forbidden, new { mensagem = ex.Message })' Controllers/DomainControllers.cs  # 16
grep -c  'BadRequest(new { mensagem = ex.Message })' Controllers/DomainControllers.cs   # 11
grep -rn 'ex.Message.Contains' --include='*.cs' .                               # DomainControllers.cs:384
grep -c  '^\[ApiController\]' Controllers/DomainControllers.cs                  # 12
grep -c  '^\[Route(' Controllers/DomainControllers.cs                           # 12
grep -c  'GetUserId()' Controllers/DomainControllers.cs                         # 23
grep -c  'GetUserRole()' Controllers/DomainControllers.cs                       # 13
grep -rn 'Roles = "' --include='*.cs' Controllers | sed -E 's/.*Roles = "([^"]+)".*/\1/' | sort | uniq -c
```

Resultado da última linha: 18 atributos `[Authorize(Roles = "...")]`, com 7 combinações distintas de strings (`Paciente` aparece 9 vezes, `Atendente,Admin` 3, `Medico` 2, e `Admin`, `Enfermeiro`, `Enfermeiro,Admin` e `Atendente,Admin,Medico,Enfermeiro` uma vez cada).

O projeto compila sem nenhum aviso no modo padrão (`dotnet build --no-incremental` em uma cópia no diretório temporário: `0 Aviso(s)`). Com os analisadores ligados (`dotnet build --no-incremental -p:AnalysisLevel=latest-all`), os avisos relevantes para este arquivo são `CA1031` (captura de `Exception` genérica) em `DomainControllers.cs:591` e `CA1307` (comparação de string sem `StringComparison`) duas vezes em `DomainControllers.cs:384`, exatamente no mapeamento de 409 por conteúdo de mensagem. Os 196 avisos `CA2007` (ConfigureAwait) são ruído em ASP.NET Core e foram desconsiderados.

## 2. Achado principal: a política de exceção para HTTP está escrita 27 vezes

O arquivo `DomainControllers.cs` tem 18 blocos `try` e 27 cláusulas `catch` (16 de `UnauthorizedAccessException`, 10 de `InvalidOperationException`, 1 de `Exception`). Cada cláusula repete o mesmo corpo: `UnauthorizedAccessException` vira `StatusCode(403, new { mensagem = ex.Message })` (16 cópias literais) e `InvalidOperationException` vira `BadRequest(new { mensagem = ex.Message })` (11 cópias literais). Uma ação típica, `ConcederAutorizacao` (`DomainControllers.cs:169-186`), gasta 18 linhas para fazer uma chamada de serviço; 11 delas são a política de erro copiada.

O pior efeito não é o volume, é a inconsistência. Como a política mora em cada ação, cada ação decide sozinha quais exceções captura, e nove ações deixam escapar exceções que os serviços lançam de propósito. Essas exceções viram HTTP 500 com stack trace no log, embora representem regras de negócio esperadas:

| Ação (DomainControllers.cs) | Exceção lançada pelo serviço | Capturada? | Status atual |
| --- | --- | --- | --- |
| `PacientesController.Criar` (92-95) | `InvalidOperationException` em `AuxiliaryServices.cs:297` | não | 500 |
| `PacientesController.ObterPorId` (97-102) | `UnauthorizedAccessException` em `AuxiliaryServices.cs:266` | não | 500 |
| `PacientesController.Atualizar` (104-106) | `InvalidOperationException` (`AuxiliaryServices.cs:330`) e `UnauthorizedAccessException` (334) | não | 500 |
| `PacientesController.ObterHistorico` (108-120) | `InvalidOperationException` "Paciente não encontrado" (`ProntuarioService.cs:180`) | só a de acesso | 500 |
| `PacientesController.ListarDocumentos` (140-152) | a mesma, via `ProntuarioService.cs:382` | só a de acesso | 500 |
| `PacientesController.ListarAutorizacoes` (154-167) | `InvalidOperationException` (`ProntuarioService.cs:513`) | só a de acesso | 500 |
| `ColetasController.Iniciar` (204-216) | `InvalidOperationException` (`TriagemChatService.cs:36` e `39`) | não | 500 |
| `ConsultasController.ObterPesquisa` (448-454) | `UnauthorizedAccessException` (`AuxiliaryServices.cs:53`) | não | 500 |
| `NotificacoesController.Listar` (547-548) | `UnauthorizedAccessException` (`AuxiliaryServices.cs:30`) | não | 500 |

Há ainda um décimo caminho: `ConsultasController.Agendar` (370-390) só trata `InvalidOperationException`, mas a proteção definitiva contra reserva dupla é o índice único parcial de `AppDbContext.cs:51-54`. Numa corrida real, o `SaveChangesAsync` de `AgendaService.cs:189` lança `DbUpdateException`, que vira 500 em vez do 409 exigido pela especificação (`Context_Prompts/prompt-poc-clinica-instrucoes-agent.md`, seção 7: "Conflito retorna 409").

O segundo problema é semântico. Os serviços usam `UnauthorizedAccessException` (31 lançamentos) para "acesso negado" e `InvalidOperationException` (38 lançamentos) para três coisas diferentes: entrada inválida, recurso inexistente (18 mensagens do tipo "não encontrado", por exemplo `ProntuarioService.cs:49`) e conflito. Como o tipo não carrega a intenção, o controller de agendamento precisa ler o texto da mensagem para descobrir o status:

```csharp
// DomainControllers.cs:382-389
catch (InvalidOperationException ex)
{
    if (ex.Message.Contains("já foi reservado") || ex.Message.Contains("mesmo intervalo"))
    {
        return StatusCode(StatusCodes.Status409Conflict, new { mensagem = ex.Message });
    }
    return BadRequest(new { mensagem = ex.Message });
}
```

Isso acopla o controller ao texto exato de duas mensagens em `AgendaService.cs:162` e `AgendaService.cs:175`. Qualquer revisão de redação em português muda o status HTTP sem que o compilador perceba. Por fim, `AdminController.CriarUsuario` (`DomainControllers.cs:587-594`) captura `Exception` e devolve 400 para qualquer falha, inclusive erros de infraestrutura.

### Proposta code-judo: uma política, nenhum try/catch

O movimento que apaga a categoria inteira é tirar a política dos controllers e colocá-la num único `IExceptionHandler` (.NET 8), mantendo o contrato de resposta `{ mensagem }` que o frontend lê em 13 pontos (`err.error?.mensagem`, ver `04_frontend.md`). Para preservar comportamento na primeira etapa, o tratador mapeia exatamente os tipos que já existem e um único tipo novo para conflito, derivado de `InvalidOperationException` para não quebrar nenhum `catch` remanescente:

```csharp
// Erros/ConflitoException.cs
public sealed class ConflitoException(string mensagem) : InvalidOperationException(mensagem);

// Erros/TratadorDeErros.cs
public sealed class TratadorDeErros : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        int? status = ex switch
        {
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ConflitoException => StatusCodes.Status409Conflict,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => null // demais exceções seguem para o tratamento padrão (500)
        };
        if (status is null) return false;
        ctx.Response.StatusCode = status.Value;
        await ctx.Response.WriteAsJsonAsync(new { mensagem = ex.Message }, ct);
        return true;
    }
}

// Program.cs
builder.Services.AddExceptionHandler<TratadorDeErros>();
builder.Services.AddProblemDetails();
app.UseExceptionHandler();
```

`AgendaService.cs:162` e `175` passam a lançar `ConflitoException`, e o `ex.Message.Contains` some. Cada ação vira uma linha:

```csharp
// antes: DomainControllers.cs:169-186 (18 linhas)
// depois:
[HttpPost("{id}/autorizacoes")]
[Authorize(Roles = Papeis.Paciente)]
public async Task<IActionResult> ConcederAutorizacao(Guid id, CriarAutorizacaoDto dto)
    => Ok(await _prontuarioService.ConcederAutorizacaoAsync(id, dto, Atual));
```

Impacto de comportamento, que precisa ser registrado como decisão e não escondido: as outras 24 ações mantêm o mesmo status e o mesmo corpo para todas as exceções que os serviços lançam hoje. As nove ações da tabela acima deixam de responder 500 e passam a responder 403 ou 400, que é o que a especificação pede; isso é uma correção visível e deve entrar no relatório como tal. Há duas outras diferenças: `AdminController.CriarUsuario` deixa de converter erros inesperados (que não vêm do Identity) em 400, e eles passam a ser 500; e as respostas 500 passam a ter corpo no formato ProblemDetails, por causa do `AddProblemDetails()`, que o `UseExceptionHandler()` sem caminho configurado exige. Numa segunda etapa, separada, vale introduzir um tipo para "não encontrado" (mapeado para 404) nas 18 mensagens hoje lançadas como `InvalidOperationException`; essa etapa muda status de 400 para 404 e deve ser feita depois que a primeira estiver verificada.

## 3. Identidade do solicitante: extraída em toda ação, re-resolvida em todo serviço

`ConsultorioControllerBase` (`DomainControllers.cs:11-37`) oferece `GetUserId`, `GetUserRole` e `GetInstituicaoId`. Os controllers chamam `GetUserId()` 23 vezes e `GetUserRole()` 13 vezes, e repassam o par `(Guid solicitanteUsuarioId, PerfilUsuario perfil)` para 11 métodos de serviço. Os serviços, por sua vez, fazem 17 consultas ao banco para descobrir qual `Paciente` ou `Medico` corresponde ao usuário (comando: `grep -rnE "(Pacientes|Medicos)\.(FirstOrDefaultAsync|FirstAsync)\(\w+ => \w+\.UsuarioId ==" Services`), por exemplo `AgendaService.cs:226`, `AgendaService.cs:232`, `ProntuarioService.cs:193` e, no mesmo método, de novo em `ProntuarioService.cs:216`.

Essa informação já está no token. `JwtTokenGenerator.cs:47-60` emite os claims `instituicaoId`, `pacienteId` e `medicoId`. O backend lê `instituicaoId` em `DomainControllers.cs:34` e `pacienteId` uma única vez, em `DomainControllers.cs:208`; `medicoId` é emitido e nunca lido. O resultado é que a mesma pergunta ("quem é o paciente deste request?") tem três respostas espalhadas: o claim, a consulta por `UsuarioId` e, no frontend, `currentUser().pacienteId`.

A extração em si também tem código morto e um padrão inseguro:

- `DomainControllers.cs:17-19` tenta `ClaimTypes.NameIdentifier`, depois `"sub"`, depois o literal `"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"`. O terceiro fallback é o valor da própria constante `ClaimTypes.NameIdentifier`, portanto nunca acrescenta nada. O mesmo vale para `DomainControllers.cs:28`, que repete o valor de `ClaimTypes.Role`.
- `DomainControllers.cs:29` devolve `PerfilUsuario.Paciente` quando o claim de papel falta ou não é reconhecido. Um default silencioso para um perfil real é o tipo de fallback que esconde um invariante; o correto é falhar.
- `AuthController.cs:47-51` reimplementa a leitura do id fora da classe base.
- Em paralelo, o gerador emite claims redundantes, que só existem para satisfazer essas cadeias de alternativas: `sub` e `NameIdentifier` com o mesmo valor (`JwtTokenGenerator.cs:38` e `41`), `Role` e `perfil` com o mesmo valor (`43` e `44`).

### Proposta code-judo: `UsuarioAtual`

Um record construído uma vez por request a partir dos claims substitui os três helpers, os 11 pares de parâmetros e as 17 consultas:

```csharp
public sealed record UsuarioAtual(
    Guid UsuarioId, PerfilUsuario Perfil, Guid? InstituicaoId, Guid? PacienteId, Guid? MedicoId)
{
    public static UsuarioAtual De(ClaimsPrincipal u) => new(
        Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Enum.Parse<PerfilUsuario>(u.FindFirstValue(ClaimTypes.Role)!),
        GuidOpcional(u, "instituicaoId"), GuidOpcional(u, "pacienteId"), GuidOpcional(u, "medicoId"));

    static Guid? GuidOpcional(ClaimsPrincipal u, string tipo)
        => Guid.TryParse(u.FindFirstValue(tipo), out var id) ? id : null;
}

// na base dos controllers (que deve virar abstract, ver seção 4)
protected UsuarioAtual Atual => UsuarioAtual.De(User);
```

Exemplo do que desaparece em `AgendaService.ListarConsultasAsync` (`AgendaService.cs:224-243`, 20 linhas e até 2 consultas extras):

```csharp
query = atual.Perfil switch
{
    PerfilUsuario.Paciente => query.Where(c => c.PacienteId == atual.PacienteId),
    PerfilUsuario.Medico => query.Where(c => c.Slot.MedicoId == atual.MedicoId),
    PerfilUsuario.Atendente or PerfilUsuario.Enfermeiro when atual.InstituicaoId is { } inst
        => query.Where(c => c.Slot.InstituicaoId == inst),
    _ => query
};
```

Preservação de comportamento: um paciente sem `pacienteId` no token produz `Where(c => c.PacienteId == null)`, que retorna lista vazia, exatamente o que `AgendaService.cs:227` faz hoje. O ponto que precisa de verificação é a defasagem do token: os claims valem pelas 24 horas do JWT. Hoje isso não muda nada, porque `Paciente` e `Medico` são criados junto com o usuário (`AuxiliaryServices.cs:302-314` e `444-453`) e `Usuario.InstituicaoId` coincide com `Medico.InstituicaoId` no seed e no cadastro pelo admin (`AuxiliaryServices.cs:422` e `448`). Se no futuro houver vínculo criado depois do login, o serviço precisará reconsultar; isso deve ficar escrito no record.

## 4. Organização do arquivo e da classe base

`DomainControllers.cs` concentra 11 controllers de oito áreas diferentes (instituições e médicos, pacientes, coletas, agenda, prontuário, notificações, dashboard, admin) em 596 linhas. Não cruza o limite de 1000 linhas, mas é um arquivo-gaveta: quem procura a rota `PUT /api/coletas/{id}/validacao` precisa saber que ela mora num arquivo chamado "DomainControllers". Depois da seção 2, o volume cai por si só (estimativa: as 18 ações com `try` perdem entre 10 e 18 linhas cada, cerca de 12 em média, o que leva o arquivo para perto de 370 linhas); nesse ponto, um arquivo por controller, agrupados por módulo, fica barato e alinhado com o "monólito modular" pedido na especificação (`Context_Prompts/prompt-poc-clinica-instrucoes-agent.md`, seção 5).

`ConsultorioControllerBase` (`DomainControllers.cs:11-13`) é `public`, não é `abstract` e herda de `ControllerBase`, que carrega `[Controller]`. Pelas regras de descoberta do MVC, ela é registrada como controller, com `[ApiController]` e `[Route("api")]`; só não expõe endpoints porque seus métodos são `protected`. Ela deve ser `abstract` e sem atributos de rota. Além disso, `[ApiController]` e `[Route("api/[controller]")]` aparecem 12 vezes cada no arquivo; `[ApiController]` é herdável, então bastaria declará-lo na base.

Os papéis são strings literais em 18 atributos. Atributos exigem constantes, e `nameof` produz constantes, então uma classe estática resolve sem mágica e sem mudar nenhuma autorização:

```csharp
public static class Papeis
{
    public const string Paciente = nameof(PerfilUsuario.Paciente);
    public const string AtendenteOuAdmin = nameof(PerfilUsuario.Atendente) + "," + nameof(PerfilUsuario.Admin);
    // ... uma constante por combinação usada hoje (7 no total)
}
```

## 5. Dependências e parâmetros mortos

- `ColetasController` injeta `AppDbContext` (`DomainControllers.cs:196-201`) e nunca o usa.
- `ColetasController.ListarFila` recebe `[FromQuery] string? status` (`DomainControllers.cs:220`) e ignora o valor; o frontend envia `?status=PendenteValidacao` (`frontend/src/app/services/api.service.ts:209`) sem efeito algum, porque o filtro está fixo em `TriagemChatService.cs:379`.
- `ConsultasController.Listar` repassa `medicoId` (`DomainControllers.cs:353-354`), e `AgendaService.ListarConsultasAsync` o recebe (`AgendaService.cs:213`) sem nunca aplicá-lo.
- Apenas `AuthController` declara `[ProducesResponseType]` (4 ocorrências) e `/// <summary>` (2). As 33 ações de `DomainControllers.cs` não documentam respostas, o que deixa o Swagger sem os códigos 400/403/404/409 que a especificação pede para documentar em OpenAPI.

## 6. HealthController

`HealthController.cs:41` usa `CountDocumentsAsync(new BsonDocument())`, uma contagem da coleção inteira, como teste de conectividade, e guarda o resultado em `var count`, que nunca é lido. Os dois `catch` genéricos (`HealthController.cs:34` e `44`, sinalizados como `CA1031`) são aceitáveis num health check, mas o ASP.NET Core já oferece `AddHealthChecks()` e `MapHealthChecks()`, e o driver do MongoDB oferece um comando `ping` barato. Isso substitui o controller inteiro por registro de configuração. A rota precisa continuar em `/api/health` para não quebrar `frontend/src/app/services/api.service.ts:315`.

## 7. Nota de modernização sobre JWT

`Program.cs:35` limpa `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap`. A partir do .NET 8, o `JwtBearer` valida tokens com `JsonWebTokenHandler` por padrão, e esse mapa do handler antigo deixa de ter efeito; o ajuste equivalente é `options.MapInboundClaims = false` dentro de `AddJwtBearer`. Hoje o sistema funciona de qualquer forma porque o token carrega tanto o nome curto quanto o URI longo dos claims (seção 3). Este item deve ser confirmado em execução antes de remover a linha, e só faz sentido junto com a simplificação dos claims redundantes.

## 8. Resumo do ganho esperado

Com o tratador único, o `UsuarioAtual` e as constantes de papel, desaparecem: 18 blocos `try`, 27 `catch`, 1 decisão por conteúdo de mensagem, 3 helpers com fallbacks mortos, 11 pares de parâmetros de identidade, 17 consultas ao banco e 18 strings de papel soltas. O que sobra em cada ação é a rota, o atributo de autorização e uma chamada de serviço.
