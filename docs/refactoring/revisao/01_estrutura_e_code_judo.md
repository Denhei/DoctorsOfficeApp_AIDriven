# 01. Estrutura, code-judo e contratos (detalhe)

Este arquivo traz a evidência dos achados 1 a 8 do resumo. Para cada achado há o que mudou, onde está, por que é um problema e um esboço de antes e depois. As linhas citadas sem prefixo de branch são de `HEAD` (o backend de `HEAD` é idêntico ao de `d483f38`). As citações `main:arquivo:linha` são do código original.

## 1. Medições

### 1.1 Como medi

Exportei as duas versões para o diretório temporário da sessão (chamado de `$S` abaixo) sem tocar no repositório e contei padrões com `grep`, sempre excluindo `Migrations/`, `bin/` e `obj/`:

```sh
git -C $R archive main  backend/Consultorio.Api | tar -x -C $S/main_src
git -C $R archive HEAD  backend/Consultorio.Api | tar -x -C $S/head_src
# medir.sh: files() = find ... -name '*.cs' -not -path '*/Migrations/*' -not -path '*/bin/*' -not -path '*/obj/*'
#           cnt()   = files | xargs grep -E -c "<padrão>" | soma
$S/medir.sh $S/main_src $S/head_src
```

Saída (resumida às linhas usadas na revisão):

```
linhas C# (sem Migrations): main=4267 head=3289
arquivos C#: main=18 head=27
\.Include\(                                main=58   head=38
\.ThenInclude\(                            main=16   head=14
new [A-Za-z]+Dto\(                         main=33   head=10
^\s*try\b                                  main=21   head=3
catch                                      main=30   head=3
StatusCode\(StatusCodes\.Status403         main=16   head=0
DateTime\.UtcNow                           main=79   head=29
Roles = "                                  main=18   head=18
UsuarioId ==                               main=17   head=2
FindAsync\(                                main=12   head=12
IConfiguration                             main=5    head=1
^public interface I                        main=10   head=10
"Bot"|"Paciente"|"Texto"|"Opcoes"          main=48   head=15
\?\? "                                     main=22   head=2
AddAsync\(|AddRangeAsync\(                 main=24   head=0
GetUserId\(\)|GetUserRole\(\)|GetInstituicaoId\(\) main=29   head=0
new MensagemMongo                          main=14   head=0
```

Em `HEAD`, as 15 ocorrências de `"Bot"|"Paciente"|"Texto"|"Opcoes"` são 9 `Roles = "Paciente"` (`DomainControllers.cs:78` a `253`) e 6 literais do modelo Mongo (`ConversaMongo.cs:51`, `55`, `65`, `67`, `73` e `75`). As 2 comparações `UsuarioId ==` restantes estão nas subconsultas da projeção de usuários (`Usuarios.cs:22-23`). Os 3 `try` restantes estão em `HealthController.cs:21` e `30` e em `Program.cs:139`.

### 1.2 Tamanho por arquivo

| Arquivo | `main` | `HEAD` |
| --- | ---: | ---: |
| `Controllers/AuthController.cs` | 61 | 47 |
| `Controllers/DomainControllers.cs` | 596 | 282 |
| `Controllers/HealthController.cs` | 62 | 53 |
| `DTOs/AuthDtos.cs` | 22 | 22 |
| `DTOs/DomainDtos.cs` | 252 | 255 |
| `DTOs/Mapeamentos.cs` | (novo) | 75 |
| `Data/AppDbContext.cs` | 135 | 135 |
| `Data/Consultas.cs` | (novo) | 17 |
| `Data/DbInitializer.cs` | 434 | 265 |
| `Data/MongoDbContext.cs` | 25 | 21 |
| `Data/Usuarios.cs` | (novo) | 57 |
| `Infraestrutura/ArmazenamentoArquivos.cs` | (novo) | 31 |
| `Infraestrutura/Opcoes.cs` | (novo) | 29 |
| `Infraestrutura/TratadorGlobalDeExcecoes.cs` | (novo) | 28 |
| `Infraestrutura/UsuarioAtual.cs` | (novo) | 31 |
| `Models/Entities/Entities.cs` | 327 | 333 |
| `Models/Enums/Enums.cs` | 47 | 47 |
| `Models/Mongo/ConversaMongo.cs` | 65 | 78 |
| `Program.cs` | 172 | 173 |
| `Services/AgendaService.cs` | 360 | 260 |
| `Services/AuthService.cs` | 105 | 33 |
| `Services/AuxiliaryServices.cs` | 470 | 272 |
| `Services/ConflitoException.cs` | (novo) | 7 |
| `Services/JwtTokenGenerator.cs` | 78 | 53 |
| `Services/ProntuarioService.cs` | 563 | 358 |
| `Services/RoteiroTriagem.cs` | (novo) | 85 |
| `Services/TriagemChatService.cs` | 493 | 242 |
| **Total** | **4.267** | **3.289** |

Nenhum arquivo cruza 1000 linhas, e nenhum cresceu de forma relevante. `Entities.cs` ganhou 6 linhas (`TemAutorizacaoAtiva`) e `ConversaMongo.cs` ganhou 13 (as fábricas de mensagem).

Os métodos de produção com mais de 40 linhas, medidos com `awk` da assinatura até o `}` de mesma indentação, são estes:

```
Data/DbInitializer.cs:15-264 (250 linhas)          SeedAsync
Services/AgendaService.cs:91-166 (76 linhas)       AgendarConsultaAsync
Services/ProntuarioService.cs:108-167 (60 linhas)  ObterHistoricoPacienteAsync
Services/ProntuarioService.cs:169-217 (49 linhas)  AnexarDocumentoAsync
Services/ProntuarioService.cs:64-106 (43 linhas)   ConcluirConsultaAsync
```

Em `main`, o maior método de domínio tinha 221 linhas (`main:Services/TriagemChatService.cs:113-333`); hoje `ProcessarMensagemAsync` tem 36 (`TriagemChatService.cs:81-116`). O seed caiu de 422 para 250 linhas.

### 1.3 Repetição que sobrou

```sh
grep -nE 'UsuarioId != (solicitante|pacienteUsuarioId)'   # regra de dono
grep -nE 'c\.Slot\.MedicoId == medico\.Id'                # vínculo médico-paciente
grep -nE 'Perfil is PerfilUsuario\.Atendente or PerfilUsuario\.Admin'
grep -nE '(PacienteId|MedicoId|InstituicaoId) \?\? throw|is not \{ \} (pacId|medId|pacienteId)'
```

| Padrão | Ocorrências | Onde |
| --- | ---: | --- |
| Regra de dono, condicional por perfil Paciente | 8 | `AgendaService.cs:101`, `214` e `240`; `TriagemChatService.cs:70`; `AuxiliaryServices.cs:189` e `222`; `ProntuarioService.cs:179` e `238` |
| Regra de dono, incondicional para paciente | 5 | `ProntuarioService.cs:131`, `269`, `308` e `327`; `TriagemChatService.cs:195` |
| Regra de dono, médico responsável | 1 | `AgendaService.cs:248` |
| Vínculo médico-paciente por consulta | 2 | `ProntuarioService.cs:141` e `247` |
| Guarda Atendente/Admin no prontuário | 2 | `ProntuarioService.cs:110` e `227` |
| Id opcional do solicitante tratado no local | 6 | `DomainControllers.cs:98`; `ProntuarioService.cs:31` e `66`; `AgendaService.cs:175` e `180`; `AuxiliaryServices.cs:85` |
| `throw new InvalidOperationException("... não encontrad...")` | 15 | `ProntuarioService.cs` (6), `TriagemChatService.cs` (4), `AgendaService.cs` (3), `AuxiliaryServices.cs` (2) |

## 2. Achado 1: a fronteira de exceções virou global e ambígua

### 2.1 O que mudou

Em `main`, cada ação decidia o que capturar. Por exemplo, `main:Controllers/DomainControllers.cs:111-119` capturava só `UnauthorizedAccessException` e devolvia 403. Ações sem `try` deixavam qualquer exceção virar 500. Em `HEAD`, `TratadorGlobalDeExcecoes.cs:14-20` vale para todo o pipeline:

```csharp
int? status = excecao switch
{
    ConflitoException => StatusCodes.Status409Conflict,
    UnauthorizedAccessException => StatusCodes.Status403Forbidden,
    InvalidOperationException => StatusCodes.Status400BadRequest,
    _ => null,
};
```

A centralização era o remédio certo, e o diagnóstico a propôs. O problema é a escolha dos tipos. `InvalidOperationException` e `UnauthorizedAccessException` não são tipos da aplicação, e muitas partes da plataforma também os lançam:

- o EF Core, em `FirstAsync` sem resultado (`ProntuarioService.cs:51`, `AuxiliaryServices.cs:270`) e em consultas LINQ que não traduzem;
- a injeção de dependência, quando não consegue montar um serviço;
- a leitura de `Nullable<T>.Value` sem valor;
- a E/S de arquivos, que lança `UnauthorizedAccessException` quando falta permissão;
- o próprio `MongoDbContext`, que lança `InvalidOperationException` para configuração ausente (`MongoDbContext.cs:13` e `15`).

Todas essas exceções agora viram 400 ou 403, com a mensagem interna no corpo da resposta.

### 2.2 Evidência

As três sondas abaixo estão descritas em `02`, seções 3 e 5.

- **Configuração ausente vira 400.** Subi a API com `MongoDatabaseName` nulo. A exceção do construtor é engolida pelo seed (`Program.cs:148-151`, log `>>> Erro ao inicializar banco de dados: MongoDatabaseName não configurado.`). Depois disso, `GET /api/health` responde `400 | application/json | {"mensagem":"MongoDatabaseName não configurado."}`.
- **Falta de permissão em disco vira 403 com caminho do servidor.** Com o diretório de upload somente leitura, o upload responde `403 | {"mensagem":"Access to the path '/var/folders/.../7d55e39d-....pdf' is denied."}`. O código original fazia o mesmo nesta rota, porque a ação capturava `UnauthorizedAccessException`. Em `HEAD`, essa regra passou a valer para todas as rotas.
- **Cada 4xx esperado vira um log de erro.** Os 37 testes HTTP geram 45 linhas `fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]`, cada uma com stack trace completo: 21 `InvalidOperationException`, 22 `UnauthorizedAccessException` e 2 `ConflitoException`. Os mesmos 37 testes contra o código original geram 4, uma para cada 500 real.

### 2.3 Remédio (verificado em parte)

Há duas mudanças independentes.

**(a) Tipos de domínio.** As 34 ocorrências de `new InvalidOperationException(` em código de aplicação (todas menos as 2 de `MongoDbContext.cs`) e as 29 de `new UnauthorizedAccessException(` mudam por substituição mecânica. A mensagem não muda, então o pin não percebe.

```csharp
// Dominio/Excecoes.cs (novo)
public abstract class ExcecaoDeDominio(string mensagem) : Exception(mensagem);
public sealed class RegraVioladaException(string mensagem) : ExcecaoDeDominio(mensagem);   // 400
public sealed class AcessoNegadoException(string mensagem) : ExcecaoDeDominio(mensagem);   // 403
public sealed class ConflitoException(string mensagem) : ExcecaoDeDominio(mensagem);       // 409 (sai de Services/)
```

```sh
grep -rl 'new InvalidOperationException(' backend/Consultorio.Api/Services backend/Consultorio.Api/Data/Usuarios.cs \
  | xargs sed -i '' 's/new InvalidOperationException(/new RegraVioladaException(/g'
grep -rl 'new UnauthorizedAccessException(' backend/Consultorio.Api/Services backend/Consultorio.Api/Infraestrutura \
  | xargs sed -i '' 's/new UnauthorizedAccessException(/new AcessoNegadoException(/g'
```

**(b) Filtro do MVC no lugar do `IExceptionHandler`.** Um `IExceptionFilter` resolve a exceção dentro do MVC, antes que ela chegue ao middleware que registra o erro. Testei esta versão numa cópia, ainda mapeando os tipos atuais:

```csharp
public sealed class FiltroExcecoesDeDominio : IExceptionFilter
{
    public void OnException(ExceptionContext ctx)
    {
        int? status = ctx.Exception switch
        {
            ConflitoException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => null,
        };
        if (status is null) return;
        ctx.Result = new ObjectResult(new { mensagem = ctx.Exception.Message }) { StatusCode = status };
        ctx.ExceptionHandled = true;
    }
}
// Program.cs: builder.Services.AddControllers(o => o.Filters.Add<FiltroExcecoesDeDominio>())
//             (sem AddExceptionHandler; UseExceptionHandler + AddProblemDetails ficam para os 500)
```

Resultado: `Aprovado! – Com falha: 0, Aprovado: 51` e `fail: 0` no log. O teste de CORS em erro (`TratamentoDeErrosTests.cs:64-79`) continua passando. Com (a) e (b) juntos, o `switch` passa a mapear só `ExcecaoDeDominio`, e qualquer exceção da plataforma volta a ser 500, registrada uma única vez.

## 3. Achado 2: o mapeamento por `ToDto()` criou um contrato oculto; projeções o apagam

### 3.1 O contrato oculto

`Mapeamentos.cs:5-8` diz: "As navegações lidas aqui precisam estar carregadas (Include) ou atribuídas pelo chamador ao construir a entidade." Esse contrato tem três consequências no código.

**Uma lista de `Include` que espelha um DTO de outra pasta.** `Data/Consultas.cs:9` documenta `ComDetalhes()` como "Carrega tudo que `Mapeamentos.ToDto(Consulta)` lê". A camada de dados conhece o formato de um DTO, e qualquer campo novo em `ConsultaDto` precisa ser lembrado em dois arquivos.

**Navegações atribuídas à mão antes do `Add`, só para o `ToDto` funcionar depois do `SaveChanges`.** Isso acontece em `AgendaService.cs:149-159` (`Paciente`, `Slot` e `Coleta`), `AgendaService.cs:75-83`, `TriagemChatService.cs:32-41`, `ProntuarioService.cs:52`, `199-211` e `282-292`. Em três desses pontos o tipo mente: `InstituicaoOrigem = instituicao!` (`ProntuarioService.cs:204`) e `origem!`/`destino!` (`287` e `289`) atribuem `null` quando a instituição não existe. Quem garante o invariante é a chave estrangeira do banco, que derruba o `SaveChanges` com 500 (sonda P4 em `02`). O `!` esconde esse caminho. Há também um acoplamento frágil, não verificado em execução: as atribuições só funcionam porque `paciente`, `slot`, `coleta` e `instituicao` vêm de consultas rastreadas pelo contexto. Se alguém acrescentar `AsNoTracking()` a uma delas, o `Add` passa a tentar inserir a entidade de novo.

**Cálculos que o SQL faria ficam em memória.** `ListarSlotsAsync` faz `Include(s => s.Consultas)` (`AgendaService.cs:27`) e calcula `disponivel` com `s.Consultas.Any(...)` em memória (`50`), carregando todas as consultas de todos os slots listados. `ComDetalhes()` carrega as linhas inteiras de `Evolucao` e `PesquisaSatisfacao` só para testar `!= null` (`Mapeamentos.cs:27-28`). `ListarPacientesAsync` regrediu: em `main` era uma projeção SQL (`main:Services/AuxiliaryServices.cs:244-257`); hoje materializa entidades e mapeia em memória (`AuxiliaryServices.cs:178-182`), porque um método de extensão não é traduzível para SQL.

Das 38 chamadas `.Include(` de `HEAD`, 26 estão em caminhos de leitura cujo único fim é montar DTO ou filtrar em memória:

| Onde | Linhas |
| --- | --- |
| `AgendaService.cs` | 25-27, 57-58, 115-116 |
| `ProntuarioService.cs` | 51, 116-122, 333-335 |
| `TriagemChatService.cs` | 137-138 |
| `Data/Consultas.cs` | 11-16 |

As outras 12 carregam dados para regras de autorização ou de estado.

### 3.2 Esboço de antes e depois

```csharp
// DTOs/Projecoes.cs (substitui Mapeamentos.cs e Data/Consultas.cs)
public static class Projecoes
{
    public static readonly Expression<Func<Consulta, ConsultaDto>> Consulta = c => new ConsultaDto(
        c.Id, c.PacienteId, c.Paciente.Nome,
        c.Slot.MedicoId, c.Slot.Medico.Usuario.NomeCompleto, c.Slot.Medico.Especialidade,
        c.Slot.InstituicaoId, c.Slot.Instituicao.Nome,
        c.SlotId, c.Slot.InicioUtc, c.Slot.FimUtc,
        c.ColetaId, c.Coleta.ResumoValidado, c.Status, c.CriadaEmUtc,
        c.Evolucao != null, c.PesquisaSatisfacao != null);

    public static readonly Expression<Func<SlotAgenda, SlotAgendaDto>> Slot = s => new SlotAgendaDto(
        s.Id, s.MedicoId, s.Medico.Usuario.NomeCompleto, s.Medico.Especialidade,
        s.InstituicaoId, s.Instituicao.Nome, s.InicioUtc, s.FimUtc,
        !s.Consultas.Any(c => c.Status != StatusConsulta.Cancelada));   // vira EXISTS no SQL
}
```

```csharp
// Antes (AgendaService.cs:24-51): 3 Include, ToListAsync e mapeamento em memória
// Depois:
public Task<List<SlotAgendaDto>> ListarSlotsAsync(Guid? medicoId, Guid? instituicaoId, DateTime? data)
    => Filtrar(context.SlotsAgenda, medicoId, instituicaoId, data)
        .OrderBy(s => s.InicioUtc)
        .Select(Projecoes.Slot)
        .ToListAsync();

// Antes (AgendaService.cs:149-165): navegações atribuídas para o ToDto funcionar
// Depois: só chaves estrangeiras, e a resposta vem da mesma projeção das leituras
var consulta = new Consulta { PacienteId = paciente.Id, SlotId = slot.Id, ColetaId = coleta.Id,
                              Status = StatusConsulta.Agendada, CriadaEmUtc = DateTime.UtcNow };
context.Consultas.Add(consulta);
await context.SaveChangesAsync();
await transaction.CommitAsync();
return await context.Consultas.Where(c => c.Id == consulta.Id).Select(Projecoes.Consulta).FirstAsync();
```

Com isso somem quatro coisas: `ComDetalhes()`, as atribuições de navegação, os três `!` e o `Include` de consultas por slot. O custo é uma leitura extra depois de cada escrita, que é desprezível nesta aplicação.

### 3.3 Ressalvas que precisam de cuidado explícito

**O histórico pina um bug que depende de um `Include` ausente.** `ProntuarioTests.cs:30-32` exige `temPesquisaRespondida = false` mesmo com a pesquisa respondida, porque `ObterHistoricoPacienteAsync` não carrega `PesquisaSatisfacao`. Uma projeção calcularia `true`. Até a correção ser aprovada, a rota de histórico precisa de `consultas.Select(c => c with { TemPesquisaRespondida = false })`, com um comentário apontando o teste. Isso é bom: o bug fica visível no código em vez de escondido na ausência de uma linha.

**A ordem das listas no histórico hoje é a ordem implícita do `Include`.** Ao projetar, é preciso fixar um `OrderBy` e conferir com a sonda P5 de `02`, que compara o JSON completo, porque o pin só verifica listas de um elemento.

Os demais DTOs de leitura (`AutorizacaoDto`, `DocumentoPacienteDto`, `PacienteDto`, `ColetaItemFilaDto` e `MedicoDto`) seguem o mesmo molde.

## 4. Achado 3: `UsuarioAtual` carrega dados mas não carrega regra

### 4.1 Evidência

Os números estão na tabela da seção 1.3: 14 cópias da regra de dono, 2 do vínculo médico-paciente, 2 da guarda Atendente/Admin e 6 tratamentos locais de id opcional. As mensagens variam por local e estão pinadas. Por exemplo, há "Acesso negado.", "Acesso negado aos dados de outro paciente." e "Você só pode agendar consultas para seu próprio cadastro.". Por isso um helper precisa receber a mensagem como parâmetro.

A política clínica ainda tem duas formas. O histórico monta um predicado por perfil (`ProntuarioService.cs:127-153`); o download repete a ideia com `if` encadeados (`227-255`). As duas discordam para o Enfermeiro. No histórico ele não vê documentos (`default: podeVerOrigem = _ => false`, `149-152`). No download ele não passa por checagem nenhuma e baixa qualquer arquivo; a sonda P5 em `02` mostra `P5 enfermeiro baixa doc | 200`, igual na base. Unificar a política deixa essa divergência explícita, o que é uma vantagem, mas ela não pode ser resolvida dentro desta refatoração, porque mudaria o comportamento.

`MedicoDeAsync` (`ProntuarioService.cs:355-357`) busca o `Medico` só para ler `InstituicaoId`. O modelo guarda a instituição do médico em dois lugares: `Usuario.InstituicaoId` (`Entities.cs:16`), que já está no claim, e `Medico.InstituicaoId` (`Entities.cs:97`). Hoje os dois recebem o mesmo valor (`AuxiliaryServices.cs:253` e `263`; `DbInitializer.cs:54`/`60` e `70`/`76`), mas nada no esquema garante isso.

### 4.2 Esboço

```csharp
public sealed record UsuarioAtual(Guid Id, PerfilUsuario Perfil, Guid? InstituicaoId, Guid? PacienteId, Guid? MedicoId)
{
    /// <summary>Paciente só acessa o que é seu; os demais perfis passam.</summary>
    public void ExigirDonoSePaciente(Guid usuarioIdDoDono, string mensagem)
    {
        if (Perfil == PerfilUsuario.Paciente && usuarioIdDoDono != Id) throw new AcessoNegadoException(mensagem);
    }

    public void ExigirDono(Guid usuarioIdDoDono, string mensagem)
    {
        if (usuarioIdDoDono != Id) throw new AcessoNegadoException(mensagem);
    }
}

// AgendaService.cs:214-217, antes:
if (solicitante.Perfil == PerfilUsuario.Paciente && consulta.Paciente.UsuarioId != solicitante.Id)
{
    throw new UnauthorizedAccessException("Acesso negado.");
}
// depois:
solicitante.ExigirDonoSePaciente(consulta.Paciente.UsuarioId, "Acesso negado.");
```

```csharp
// Services/PoliticaClinica.cs: o mesmo cálculo para histórico e download, mensagens por chamador
public sealed record MensagensNegacao(string Perfil, string OutroPaciente, string SemVinculo);

public async Task<Func<Guid, bool>?> OrigensVisiveisAsync(Paciente paciente, UsuarioAtual s, MensagensNegacao msg)
{
    switch (s.Perfil)
    {
        case PerfilUsuario.Atendente or PerfilUsuario.Admin:
            throw new AcessoNegadoException(msg.Perfil);
        case PerfilUsuario.Paciente:
            s.ExigirDono(paciente.UsuarioId, msg.OutroPaciente);
            return _ => true;
        case PerfilUsuario.Medico:
            var medico = await context.Medicos.FindAsync(s.MedicoId) ?? throw new AcessoNegadoException("Médico não encontrado.");
            if (!await context.Consultas.AnyAsync(c => c.PacienteId == paciente.Id && c.Slot.MedicoId == medico.Id))
                throw new AcessoNegadoException(msg.SemVinculo);
            return origem => origem == medico.InstituicaoId || paciente.TemAutorizacaoAtiva(origem, medico.InstituicaoId);
        default:
            return null;   // Enfermeiro: o histórico trata null como "nada"; o download, como "tudo" (comportamento atual)
    }
}
```

Este esboço remove o segundo vínculo, a segunda guarda e o segundo `MedicoDeAsync`. O `null` do `default` documenta, num lugar só, a divergência do Enfermeiro, que deve ir para a lista de bugs.

## 5. Achado 4: regra de negócio nos controllers

Três ações ainda decidem regra em vez de só traduzir HTTP.

**`ColetasController.ListarFila`** (`DomainControllers.cs:108-116`) recebe `string? status` e o ignora (bug conhecido). Também decide quem pode ver a fila e passa `Guid.Empty` como sentinela quando o Admin não tem instituição. O pin congela a lista vazia em `TriagemAgendamentoTests.cs:138-140`.

**`SlotsController.Criar`** (`162`) escolhe `Solicitante.InstituicaoId ?? dto.InstituicaoId`.

**`ColetasController.Iniciar`** (`98-101`) devolve `Unauthorized()` (401) quando o paciente não tem registro. Para o mesmo usuário, `NotificacaoPesquisaService` lança `UnauthorizedAccessException` (403) em `AuxiliaryServices.cs:84-85`. A sonda P2 mostra `P2 iniciar coleta | 401` e `P2 notificacoes | 403`.

Como todo serviço já recebe `UsuarioAtual`, essas decisões podem descer sem mudar o comportamento:

```csharp
// Antes (DomainControllers.cs:106-116)
public async Task<IActionResult> ListarFila([FromQuery] string? status)
{
    if (Solicitante.InstituicaoId is null && Solicitante.Perfil != PerfilUsuario.Admin) return Forbid();
    return Ok(await chatService.ListarFilaEnfermeiroAsync(Solicitante.InstituicaoId ?? Guid.Empty));
}
// Depois: o serviço conhece a regra; o controller só traduz "negado sem corpo" em Forbid()
public async Task<IActionResult> ListarFila([FromQuery] string? status)
    => await chatService.ListarFilaAsync(Solicitante) is { } fila ? Ok(fila) : Forbid();
```

Retornos `bool` traduzidos no controller:

| Método | Controller | Pode virar exceção? |
| --- | --- | --- |
| `ValidarColetaAsync` | `DomainControllers.cs:139-145`: `false` vira `BadRequest(new { mensagem = "Coleta não encontrada ou já validada." })` | Sim. `RegraVioladaException` com a mesma mensagem gera o mesmo status e o mesmo corpo `{ mensagem }`, e o `bool` e o `if` somem. |
| `AtualizarStatusConsultaAsync` | `192-193`: `false` vira `BadRequest()` vazio | Não sem mudar o contrato. O `BadRequest()` vazio vira ProblemDetails, e o pin congela `application/problem+json` (`TriagemAgendamentoTests.cs:259`). |
| `RevogarAutorizacaoAsync` | `245-246`: `false` vira `NotFound()` vazio | Não sem mudar o contrato, pelo mesmo motivo (corpo ProblemDetails de 404). |

## 6. Achado 5: contratos de fronteira como strings

**Claims.** `JwtTokenGenerator.cs:33-35` emite `"instituicaoId"`, `"pacienteId"` e `"medicoId"`; `UsuarioAtual.cs:24-26` lê os mesmos literais. O claim `"perfil"` (`JwtTokenGenerator.cs:30`) perdeu o leitor no commit `75f3a92` (`git show 75f3a92`: "três helpers com fallbacks mortos (... claim "perfil") viram uma propriedade"). O frontend não decodifica o token: `grep -rnE "atob|jwt|decode|split\('\.'\)" frontend/src` não encontra nada. O token também carrega `sub` e `NameIdentifier` com o mesmo valor (`24` e `27`). O remédio é deixar os dois sentidos no mesmo tipo:

```csharp
public sealed record UsuarioAtual(...)
{
    private const string Instituicao = "instituicaoId", Paciente = "pacienteId", Medico = "medicoId";
    public static UsuarioAtual De(ClaimsPrincipal p) { /* lê as constantes */ }
    public IEnumerable<Claim> ParaClaims() { /* emite as mesmas constantes; usado por JwtTokenGenerator */ }
}
```

Remover `perfil` e `sub` é opcional. Nenhum código lê esses claims, mas o conteúdo do token muda.

**Papéis.** Há 18 `Roles = "..."` com 7 combinações (`grep -hoE 'Roles = "[^"]*"' | sort | uniq -c`: `Paciente` 9, `Atendente,Admin` 3, `Medico` 2, e `Admin`, `Enfermeiro`, `Enfermeiro,Admin` e `Atendente,Admin,Medico,Enfermeiro` uma vez cada). Um atributo de seis linhas faz do enum a fonte única e elimina a classe de erro por digitação:

```csharp
public sealed class PerfisAttribute : AuthorizeAttribute
{
    public PerfisAttribute(params PerfilUsuario[] perfis) => Roles = string.Join(",", perfis);
}
// [Authorize(Roles = "Atendente,Admin")]  vira  [Perfis(PerfilUsuario.Atendente, PerfilUsuario.Admin)]
```

**Mensagens do Mongo.** `Remetente` e `Tipo` são strings com o domínio de valores escrito em comentário (`ConversaMongo.cs:51` e `55`). Com enums, o armazenamento e o JSON continuam iguais. O `JsonStringEnumConverter` já é global (`Program.cs:97`), e `[BsonRepresentation(BsonType.String)]` grava o nome. Antes de mudar, vale acrescentar ao pin a leitura de uma conversa antiga (a do seed, coleta `44444444-...`, que nenhum teste lê hoje) para provar que os documentos gravados continuam legíveis.

```csharp
public enum Remetente { Bot, Paciente }
public enum TipoMensagem { Texto, Opcoes }
[BsonRepresentation(BsonType.String)] public Remetente Remetente { get; set; } = Remetente.Bot;
[BsonRepresentation(BsonType.String)] public TipoMensagem Tipo { get; set; } = TipoMensagem.Texto;
```

**Etapas do roteiro.** `RoteiroTriagem.Avancar` e `Pergunta` usam os inteiros 1 a 9 (`RoteiroTriagem.cs:26-79`). Os testes de unidade repetem os números (`RoteiroTriagemTests.cs:11-21`). Um `enum EtapaTriagem { ConfirmacaoDados = 1, Queixa, Duracao, Alergias, DetalheAlergias, Medicamentos, DetalheMedicamentos, Observacoes, Resumo }` torna os dois `switch` legíveis. O enum fica gravado como inteiro no Mongo, porque esse é o padrão do driver para enums. A API precisa continuar devolvendo `etapaAtual` como número (`TriagemAgendamentoTests.cs:39`, `61` e outros), então `ConversaDetalheDto.EtapaAtual` continua `int`, com conversão no mapeamento; se o DTO usasse o enum, o conversor global passaria a serializar texto.

Sobre trocar o `switch` por uma tabela: não recomendo. `Avancar` tem quatro formatos de transição (confirmação, texto livre, sim/não/não sei e observação), e uma tabela pediria dois combinadores para economizar poucas linhas. O acoplamento que importa é outro: o texto das opções está em `Pergunta` (`69-76`) e o casamento por trecho está em `Avancar` (`Contem(resposta, "Sim")`, `38` e `47`). Constantes compartilhadas para as opções tornam esse vínculo explícito. O bug "assim" contém "sim" (`RoteiroTriagem.cs:84` usa `OrdinalIgnoreCase`, como em `main:Services/TriagemChatService.cs:199`) continua preservado de propósito.

**Configuração do Mongo.** `MongoDbContext.cs:10-18` ainda recebe `IConfiguration` e lança `InvalidOperationException` na primeira resolução. Como o seed engole a falha (`Program.cs:148-151`), o erro aparece depois, em cada request, como 400 (seção 2.2). O padrão que a branch criou para JWT, com `AddOptions<JwtOptions>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()` (`Program.cs:17`), resolve isso:

```csharp
public sealed class MongoOptions
{
    [Required] public string ConnectionString { get; init; } = "";
    [Required] public string DatabaseName { get; init; } = "";
}
builder.Services.AddOptions<MongoOptions>()
    .Configure<IConfiguration>((o, c) => { /* lê ConnectionStrings:MongoConnection e MongoDatabaseName */ })
    .ValidateDataAnnotations().ValidateOnStart();
```

**Tupla no download.** `Task<(byte[] Bytes, string ContentType, string NomeArquivo)?>` (`ProntuarioService.cs:18` e `225`) obriga o controller a escrever `res.Value.Bytes` três vezes (`DomainControllers.cs:232`). Um `record ArquivoDocumento(byte[] Conteudo, string ContentType, string Nome)` com retorno anulável remove o `.Value`.

## 7. Achado 6: decomposição

| Arquivo | Linhas | Tipos | Proposta |
| --- | ---: | ---: | --- |
| `Controllers/DomainControllers.cs` | 282 | 12 (base + 11 controllers) | Um arquivo por controller. Movimento puro, sem risco. |
| `Services/AuxiliaryServices.cs` | 272 | 10 (5 interfaces + 5 serviços) | Um arquivo por serviço: `NotificacaoPesquisaService`, `DashboardService`, `InstituicaoService`, `PacienteService` e `AdminService`. |
| `Services/ProntuarioService.cs` | 358 | 2 | Dividir por assunto: `AtendimentoService` (evolução e conclusão), `HistoricoService`, `DocumentoService` e `AutorizacaoService`, mais a `PoliticaClinica` da seção 4. |
| `Models/Entities/Entities.cs` | 333 | 13 | Opcional, um arquivo por entidade. |
| `DTOs/DomainDtos.cs` | 255 | 28 | Opcional, por área (agenda, prontuário, triagem). |

O diagnóstico (`docs/refactoring/diagnostico/00_resumo.md`, passo 9) previa a divisão para depois do encolhimento. O encolhimento aconteceu, então o diff de movimentação agora é o menor possível. `ListarDocumentosAsync` (`ProntuarioService.cs:219-223`) é um sintoma de `ProntuarioService` acumular assuntos: para devolver documentos, carrega o grafo inteiro do histórico (consultas, evoluções, slots, médicos e autorizações), comportamento herdado de `main:Services/ProntuarioService.cs:380-384`.

`DbInitializer.SeedAsync` tem 250 linhas num método só (`DbInitializer.cs:15-264`). Como o conteúdo é quase todo declarativo, isso é aceitável. Uma divisão em `SemearEquipeAsync`, `SemearHistoricoCarlosAsync` e `SemearAgenda` melhoraria a navegação sem mudar dados.

## 8. Achado 7: modularidade

**Interfaces de implementação única.** `grep -c '^public interface I'` dá 10 em `main` e 10 em `HEAD`, somando 66 linhas (`awk` do `public interface` até o `}`). Nenhum teste usa dublê. O pin sobe a API inteira (`ApiFixture.cs:45-49`), e os testes de unidade chamam a classe estática `RoteiroTriagem` (`RoteiroTriagemTests.cs:23`). Registrar as classes concretas (`AddScoped<AgendaService>()`) e injetá-las apaga as 66 linhas e o salto de navegação da interface para a classe.

**`Data/Usuarios.cs` mistura dois assuntos.** `ProjetarInfo` (`14-23`) é uma projeção de leitura que recebe o `AppDbContext` para fazer subconsultas correlacionadas. `CriarUsuarioAsync` (`26-56`) é um comando de escrita sobre `UserManager` que descarta o resultado de `AddToRoleAsync` (`54`). O parâmetro `db` existe porque o modelo declara `Paciente → Usuario` e `Medico → Usuario` como 1:N (`WithMany()` em `Migrations/AppDbContextModelSnapshot.cs:745-749` e `775-779`), apesar dos índices únicos (`AppDbContext.cs:34-40`). Com navegações inversas 1:1 (`HasOne(p => p.Usuario).WithOne(u => u.Paciente)`), a projeção vira `u.Paciente != null ? u.Paciente.Id : null` e deixa de precisar do contexto. Isso provavelmente não muda o esquema, mas é preciso confirmar gerando uma migration de verificação e conferindo que `Up()` e `Down()` ficam vazios.

**Ciclo entre pastas.** `Infraestrutura/TratadorGlobalDeExcecoes.cs:1` importa `Consultorio.Api.Services` por causa de `ConflitoException`, e os serviços importam `Consultorio.Api.Infraestrutura` por causa de `UsuarioAtual` (por exemplo `ProntuarioService.cs:3`). O solicitante e as exceções de regra são conceitos de domínio. Uma pasta `Dominio/` desfaz o ciclo, e `Infraestrutura` fica com o que é de fato infraestrutura: leitura de claims, disco, opções e o filtro HTTP.

**"Um ponto de montagem por DTO" ficou parcial.** O commit `a8476ae` promete isso, mas `ConversaDetalheDto` é montado em `TriagemChatService.cs:215-241`, `ColetaItemFilaDto` em `147-156`, `UsuarioInfoDto` em `Data/Usuarios.cs:15-23`, e `InstituicaoDto`/`MedicoDto` dentro dos serviços (`AuxiliaryServices.cs:143` e `155-163`). Isso não é grave, porque cada um tem uma montagem só, mas o leitor precisa procurar em quatro pastas. Na proposta da seção 3, todas as projeções vão para `Projecoes.cs`, inclusive `MapearDetalheDto`, que combina dados do PostgreSQL e do Mongo e pode virar `ToDto(this ConversaMongo conversa, ColetaInicial coleta)`.

## 9. Achado 8: legibilidade

- **Atributos repetidos.** Os 11 controllers repetem `[ApiController]` e `[Route("api/[controller]")]` (`DomainControllers.cs:20-21` a `271-272`). A base já é `abstract` (`12`) e por isso não é descoberta como controller; os atributos poderiam subir para ela. Não verifiquei em execução. O cuidado é com `AdminController`, que declara `[Route("api/admin")]`: ele precisaria perder a rota própria para não ganhar duas. O pin detectaria qualquer quebra.
- **Cache sem efeito.** O `_solicitante ??=` (`DomainControllers.cs:14-16`) não tem efeito prático: cada ação lê `Solicitante` uma vez, exceto `ListarFila`, que lê três vezes, e montar o record custa três `TryParse`.
- **Pré-condição silenciosa.** `Paciente.TemAutorizacaoAtiva` (`Entities.cs:80-84`) está no agregado certo, mas devolve `false` em silêncio se `Autorizacoes` não estiver carregado. Hoje os dois chamadores carregam a coleção (`ProntuarioService.cs:121-122` e `233`). Como falha fechado, a pré-condição merece um comentário.

## 10. O que a branch fez bem

Para equilíbrio, o que deve ser mantido como está:

- `RoteiroTriagem` como função pura, com perguntas indexadas pela etapa de chegada (`RoteiroTriagem.cs:65-80`); as duplicações literais de `main:Services/TriagemChatService.cs:221/236` e `266/281` sumiram por construção.
- `ProcessarMensagemAsync` caiu de 221 para 36 linhas.
- As fábricas `MensagemMongo.Bot`/`DoPaciente` (`ConversaMongo.cs:63-77`).
- A fila do enfermeiro com uma única consulta `$in` (`TriagemChatService.cs:143-145`).
- O `GroupBy` do dashboard (`AuxiliaryServices.cs:109-112`).
- `JwtOptions`, compartilhada entre assinatura e validação, com `ValidateOnStart` (`Program.cs:17` e `47-63`).
- `ArmazenamentoArquivos` como singleton fora do domínio.
- DTOs de entrada como records com valores padrão.
- O seed sem os nove blocos repetidos.
- `ConflitoException` no lugar do `Contains("já foi reservado")` (`main:Controllers/DomainControllers.cs:384`).
