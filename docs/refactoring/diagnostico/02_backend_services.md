# 02. Backend: serviços de domínio

Linha de base: commit `8ff4b1d` (branch `main`). Caminhos relativos a `backend/Consultorio.Api/`. Medições feitas sobre `git archive main`.

## 1. Medições

| Arquivo | Linhas | Conteúdo |
| --- | ---: | --- |
| `Services/ProntuarioService.cs` | 563 | 1 interface + 1 classe (9 métodos públicos) |
| `Services/TriagemChatService.cs` | 493 | 1 interface + 1 classe (6 métodos públicos) |
| `Services/AuxiliaryServices.cs` | 470 | 5 interfaces + 5 classes sem relação entre si |
| `Services/AgendaService.cs` | 360 | 1 interface + 1 classe (6 métodos públicos) |
| `Services/AuthService.cs` | 105 | 1 interface + 1 classe |
| `Services/JwtTokenGenerator.cs` | 78 | 1 interface + 1 classe |

Métodos com 40 linhas ou mais (script de contagem por casamento de chaves, reproduzido no apêndice e executado como `python3 metodos.py backend 40`; os intervalos foram conferidos à mão; o seed e o `OnModelCreating` estão em `03_backend_config_dados_seed.md`):

| Linhas | Método | Local |
| ---: | --- | --- |
| 221 | `ProcessarMensagemAsync` | `TriagemChatService.cs:113-333` |
| 148 | `ObterHistoricoPacienteAsync` | `ProntuarioService.cs:163-310` |
| 98 | `AgendarConsultaAsync` | `AgendaService.cs:114-211` |
| 69 | `ConcluirConsultaAsync` | `ProntuarioService.cs:93-161` |
| 67 | `AnexarDocumentoAsync` | `ProntuarioService.cs:312-378` |
| 64 | `ListarConsultasAsync` | `AgendaService.cs:213-276` |
| 57 | `CriarUsuarioAsync` | `AuxiliaryServices.cs:413-469` |
| 55 | `IniciarColetaAsync` | `TriagemChatService.cs:33-87` |
| 55 | `GerarToken` | `JwtTokenGenerator.cs:23-77` |
| 54 | `ObterArquivoParaDownloadAsync` | `ProntuarioService.cs:386-439` |
| 53 | `ValidarColetaAsync` | `TriagemChatService.cs:405-457` |
| 52 | `RegistrarEvolucaoAsync` | `ProntuarioService.cs:40-91` |

Duplicações contadas (comandos executados em `backend/Consultorio.Api/`):

```bash
for t in ConsultaDto AutorizacaoDto PacienteDto UsuarioInfoDto DocumentoPacienteDto EvolucaoDto \
         PesquisaSatisfacaoDto SlotAgendaDto ColetaItemFilaDto; do
  echo "$t: $(grep -rn "new $t(" --include='*.cs' Services | cut -d: -f1,2 | tr '\n' ' ')"; done
grep -rn  "new MensagemMongo" --include='*.cs' Services | wc -l                         # 14
grep -rn  'Remetente = "Bot"' --include='*.cs' Services Data | wc -l                     # 17 (14 serviço + 3 seed)
grep -rhoE 'Tipo = "[A-Za-z]+"' --include='*.cs' Services Data | sort | uniq -c          # 14 "Texto", 7 "Opcoes"
grep -rn  "ConversaId.ToString()" --include='*.cs' Services | wc -l                      # 5
grep -rnE "(Pacientes|Medicos)\.(FirstOrDefaultAsync|FirstAsync)\(\w+ => \w+\.UsuarioId ==" Services | wc -l   # 17
grep -rn  "ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)" Services | wc -l     # 4
grep -rno '\.Include(' Services | wc -l                                                  # 58
grep -rn  "a.RevogadaEmUtc == null" Services | wc -l                                      # 6
grep -rn  "EtapaEsperada\|VersaoRoteiro" --include='*.cs' . | grep -v Migrations          # só declarações e 1 escrita
```

Construções manuais de DTO: `ConsultaDto` 4 vezes (`AgendaService.cs:192`, `257`, `303`; `ProntuarioService.cs:543`), `PacienteDto` 4 (`AuxiliaryServices.cs:247`, `269`, `316`, `345`), `UsuarioInfoDto` 4 (`AuthService.cs:57`, `94`; `AuxiliaryServices.cs:398`, `459`), `AutorizacaoDto` 3 (`ProntuarioService.cs:288`, `474`, `527`), e 2 cada para `DocumentoPacienteDto`, `EvolucaoDto`, `PesquisaSatisfacaoDto` e `SlotAgendaDto`. São 24 construções escritas à mão para 9 tipos, cada uma com 6 a 17 argumentos posicionais.

## 2. Triagem: o roteiro está escrito como um `switch` de 167 linhas

`ProcessarMensagemAsync` (`TriagemChatService.cs:113-333`) tem 221 linhas, e 167 delas são o `switch (conversa.EtapaAtual)` de `TriagemChatService.cs:157-323`. Cada ramo mistura três coisas que mudam por motivos diferentes: a transição de estado (`conversa.EtapaAtual = N`), o preenchimento do resumo e o texto da próxima pergunta, escrito como um literal completo de `MensagemMongo`. O serviço cria 14 literais `new MensagemMongo` (11 dentro do `switch`, mais `146`, `359` e `443`) e um `new()` em `TriagemChatService.cs:70`; os 14 de bot repetem `Remetente = "Bot"`, `Id = Guid.NewGuid().ToString()` e `EnviadaEmUtc = DateTime.UtcNow`, três valores que já são os inicializadores padrão da classe (`Models/Mongo/ConversaMongo.cs:52`, `54` e `64`).

Como a pergunta é emitida no ramo de origem, dois ramos que chegam à mesma etapa duplicam o texto. A pergunta de medicamentos está escrita duas vezes, idêntica, em `TriagemChatService.cs:221-223` e `236-238`; a pergunta de observações também, em `266-268` e `281-283`. Qualquer ajuste de redação precisa ser feito em dois lugares.

A ramificação depende do conteúdo textual da resposta: `Contains("Sim")` em `199` e `244`, `Contains("Não sei")` em `213` e `258`, `Contains("Não tenho")` em `289`. O protocolo entre bot e paciente é, na prática, o rótulo do botão, que o frontend devolve como texto (`frontend/src/app/components/paciente/chat-triagem/chat-triagem.component.ts:313-316`). Os números das etapas são literais em 12 atribuições (`64`, `160`, `173`, `186`, `201`, `216`, `231`, `246`, `261`, `276`, `298`, além do seed em `Data/DbInitializer.cs:253`), e o número 9 também está duplicado no template do frontend (`chat-triagem.component.ts:70` e `103`).

### Proposta code-judo: o roteiro como dado, com a pergunta indexada pela etapa de destino

A reorganização que apaga as duplicações não é extrair métodos de cada `case`; é separar "transição" de "pergunta" e indexar a pergunta pela etapa de chegada. Assim cada pergunta existe uma vez, por construção:

```csharp
// Triagem/RoteiroTriagem.cs
public static class RoteiroTriagem
{
    public const int ProntoParaEnvio = 9;
    public const string ForaDoRoteiro = "Nesta demonstração, consigo coletar informações para a equipe. Para outras dúvidas, solicite atendimento humano.";

    // Transição: aplica a resposta da etapa atual e devolve a próxima.
    public static int Responder(int etapa, string texto, ResumoPreliminarMongo r)
    {
        switch (etapa)
        {
            case 1: return 2;
            case 2: r.Queixa = OuNaoInformado(texto); return 3;
            case 3: r.Duracao = OuNaoInformado(texto); return 4;
            case 4 when Afirmativo(texto): return 5;
            case 4: r.Alergias = NaoSabe(texto) ? "Não sabe informar" : "Nenhuma relatada"; return 6;
            case 5: r.Alergias = OuNaoInformado(texto); return 6;
            case 6 when Afirmativo(texto): return 7;
            case 6: r.Medicamentos = NaoSabe(texto) ? "Não sabe informar" : "Nenhum relatado"; return 8;
            case 7: r.Medicamentos = OuNaoInformado(texto); return 8;
            case 8:
                r.Observacoes = texto.Contains("Não tenho", StringComparison.OrdinalIgnoreCase)
                    ? "Nenhuma observação adicional" : texto;
                return ProntoParaEnvio;
            default: return etapa;
        }
    }

    // Pergunta: exibida ao chegar na etapa. Cada texto existe uma única vez.
    public static MensagemMongo Pergunta(int etapa, string instituicao, ResumoPreliminarMongo r) => etapa switch
    {
        2 => MensagemMongo.DoBot($"Perfeito! Seu atendimento será realizado na unidade: **{instituicao}**. ..."),
        3 => MensagemMongo.DoBot("Entendido. **Há quanto tempo** você está sentindo esses sintomas? ..."),
        4 => MensagemMongo.DoBot("Você possui algum tipo de **alergia** conhecida ...?",
                                 "Não possuo alergias", "Sim, possuo alergia", "Não sei informar"),
        5 => MensagemMongo.DoBot("Por favor, digite a quais substâncias ou medicamentos você tem alergia:"),
        6 => MensagemMongo.DoBot("Você está fazendo **uso de algum medicamento** contínuo ou recente?",
                                 "Não tomo medicamentos", "Sim, tomo medicamentos", "Não sei informar"),
        7 => MensagemMongo.DoBot("Por favor, digite o nome dos medicamentos e dosagem (se souber):"),
        8 => MensagemMongo.DoBot("Deseja acrescentar mais alguma **informação ou observação** ...?",
                                 "Não tenho observações adicionais"),
        ProntoParaEnvio => MensagemMongo.DoBot($"📋 **Resumo da Coleta Preliminar:**\n\n{r.GerarTextoFormatado()}\n\n...",
                                 "Confirmar e Enviar para o Enfermeiro"),
        _ => throw new ArgumentOutOfRangeException(nameof(etapa))
    };

    static bool Afirmativo(string t) => t.Contains("Sim", StringComparison.OrdinalIgnoreCase); // mesma regra de hoje
    static bool NaoSabe(string t) => t.Contains("Não sei", StringComparison.OrdinalIgnoreCase);
    static string OuNaoInformado(string t) => string.IsNullOrWhiteSpace(t) ? "não informado" : t;
}

// ConversaMongo.cs
public static MensagemMongo DoBot(string conteudo, params string[] opcoes) => new()
{
    Conteudo = conteudo,
    Tipo = opcoes.Length > 0 ? "Opcoes" : "Texto",
    Opcoes = opcoes.Length > 0 ? [.. opcoes] : null
};
```

O corpo do `switch` atual some de `ProcessarMensagemAsync`, que fica com a orquestração:

```csharp
conversa.Mensagens.Add(MensagemMongo.DoPaciente(texto, dto.ClienteMessageId));
if (conversa.EtapaAtual >= RoteiroTriagem.ProntoParaEnvio)
    conversa.Mensagens.Add(MensagemMongo.DoBot(RoteiroTriagem.ForaDoRoteiro));
else
{
    conversa.EtapaAtual = RoteiroTriagem.Responder(conversa.EtapaAtual, texto, conversa.ResumoPreliminar);
    conversa.Mensagens.Add(RoteiroTriagem.Pergunta(conversa.EtapaAtual, coleta.Instituicao.Nome, conversa.ResumoPreliminar));
}
```

Preservação de comportamento: o ramo `default` atual (`TriagemChatService.cs:312-322`) só é alcançado na etapa 9, porque as etapas válidas vão de 1 a 9 e não existe `case 9`; a condição `>= ProntoParaEnvio` reproduz isso. `Tipo` é derivável de `Opcoes` em todas as 14 mensagens de bot (todas as mensagens `"Opcoes"` têm lista, nenhuma `"Texto"` tem), então `DoBot` produz os mesmos documentos. As regras `Contains("Sim")` e `Contains("Não sei")` foram mantidas de propósito; o fato de "assim" conter "sim" é um defeito e está na lista de bugs do resumo, não nesta refatoração. Estimativa: `ProcessarMensagemAsync` cai de 221 para cerca de 35 linhas e o roteiro inteiro cabe em cerca de 70 linhas de um arquivo próprio.

## 3. O documento Mongo carrega estado duplicado e campos mortos

`ConversaMongo.Status` (`ConversaMongo.cs:25`) é escrito em `TriagemChatService.cs:66` ("EmAndamento"), `358` ("PendenteValidacao") e `438` (`coleta.Status.ToString()`, isto é, "Validada" ou "EncaminhadaPresencial"), e no seed como "Concluida" (`DbInitializer.cs:254`). Ele nunca é lido: `MapearDetalheDto` usa `coleta.Status` do PostgreSQL (`TriagemChatService.cs:485`), e nenhum outro código consulta o campo. O comentário de `ConversaMongo.cs:25` lista "EmAndamento, Concluida, Cancelada", valores que não batem com o que o código grava. É a mesma informação em dois bancos, exatamente o que a especificação pede para evitar ("evite duplicar a mesma fonte de verdade nos dois", `Context_Prompts/prompt-gerar-especificacao-poc-clinica.md:65`).

`VersaoRoteiro` (`ConversaMongo.cs:23`) é gravado uma vez (`TriagemChatService.cs:65`) e nunca lido. `EnviarMensagemChatDto.EtapaEsperada` (`DTOs/DomainDtos.cs:38`) nunca é lido pelo backend nem enviado pelo frontend. Os dois existiam para cumprir o "controle de versão da etapa" que a especificação exige contra requisições repetidas (`prompt-poc-clinica-instrucoes-agent.md`, seção 8); hoje são código morto. O comentário de `ConversaMongo.cs:58` cita um tipo `"Resumo"` que nunca é usado.

`ColetaInicial.ConversaId` é `Guid` (`Models/Entities/Entities.cs:146`) e `ConversaMongo.Id` é `string` (`ConversaMongo.cs:10`). Por isso o serviço repete `Find(c => c.Id == coleta.ConversaId.ToString())` cinco vezes (`TriagemChatService.cs:105`, `132`, `349`, `387`, `433`). Um método privado `BuscarConversaAsync(Guid conversaId)` concentra a conversão.

Remoção segura: apagar `Status` e `VersaoRoteiro` da classe exige marcar `ConversaMongo` com `[BsonIgnoreExtraElements]` (hoje há 0 ocorrências no projeto). Sem isso, o driver do MongoDB lança erro ao desserializar os documentos já gravados, que continuam tendo esses campos. Para `Remetente` e `Tipo`, enums com `[BsonRepresentation(BsonType.String)]` gravam exatamente os mesmos textos ("Bot", "Paciente", "Texto", "Opcoes"), então os documentos existentes continuam compatíveis.

## 4. Mapeamento manual e cadeias de `Include`

As 24 construções manuais de DTO dependem de cadeias de `Include` para que as navegações estejam carregadas. Há 58 chamadas `.Include(` nos serviços, e a cadeia `Include(c => c.Slot).ThenInclude(s => s.Medico).ThenInclude(m => m.Usuario)` aparece 4 vezes (`AgendaService.cs:220`, `285`; `ProntuarioService.cs:105`, `174`). Quando a cadeia e o mapeamento ficam em lugares diferentes, a única forma de saber se o DTO vai funcionar é ler os dois.

`ProntuarioService.MapearConsultaDto` (`ProntuarioService.cs:541-562`) é a quarta versão do mapeamento de `ConsultaDto` e usa fallbacks como `?? "Médico"`, `?? Guid.Empty` e `?? DateTime.MinValue` (`546-554`). Nos dois chamadores esses fallbacks nunca disparam: `ConcluirConsultaAsync` carrega todas as navegações em `ProntuarioService.cs:101-106`, e o histórico as carrega em `172-175`, com o EF preenchendo `c.Paciente` por fix-up. Os fallbacks só servem para esconder o invariante; se um dia faltar um `Include`, o sistema devolve "Médico" e `0001-01-01` em vez de falhar. `AgendaService` não reutiliza esse método e escreve as outras três cópias por conta própria.

`ListarSlotsAsync` (`AgendaService.cs:28-66`) faz `Include(s => s.Consultas)` (`33`) e carrega todas as consultas de todos os slots só para calcular `Disponivel` em memória (`64`).

### Proposta code-judo: uma projeção por DTO

```csharp
// DTOs/Projecoes.cs
public static class Projecoes
{
    public static readonly Expression<Func<Consulta, ConsultaDto>> Consulta = c => new ConsultaDto(
        c.Id, c.PacienteId, c.Paciente.Nome, c.Slot.MedicoId, c.Slot.Medico.Usuario.NomeCompleto,
        c.Slot.Medico.Especialidade, c.Slot.InstituicaoId, c.Slot.Instituicao.Nome, c.SlotId,
        c.Slot.InicioUtc, c.Slot.FimUtc, c.ColetaId, c.Coleta.ResumoValidado, c.Status,
        c.CriadaEmUtc, c.Evolucao != null, c.PesquisaSatisfacao != null);

    public static readonly Func<Consulta, ConsultaDto> ConsultaEmMemoria = Consulta.Compile();

    public static readonly Expression<Func<SlotAgenda, SlotAgendaDto>> Slot = s => new SlotAgendaDto(
        s.Id, s.MedicoId, s.Medico.Usuario.NomeCompleto, s.Medico.Especialidade, s.InstituicaoId,
        s.Instituicao.Nome, s.InicioUtc, s.FimUtc, !s.Consultas.Any(c => c.Status != StatusConsulta.Cancelada));
}

// AgendaService.ListarConsultasAsync, depois do filtro por perfil:
return await query.OrderByDescending(c => c.Slot.InicioUtc).Select(Projecoes.Consulta).ToListAsync();
```

Com `Select`, o EF gera os joins sozinho e as cadeias de `Include` dos caminhos de leitura desaparecem; em `ListarSlotsAsync`, a disponibilidade vira um `EXISTS` no SQL em vez de carregar consultas. Nos caminhos de escrita (`AgendarConsultaAsync`, `ConcluirConsultaAsync`), a alternativa mais simples é reler o registro pela mesma projeção depois do `SaveChangesAsync`, ao custo de uma consulta. O número de definições de mapeamento cai de 24 para 9, uma por tipo.

Um cuidado que o roteiro de caracterização vai acusar: o histórico não carrega `PesquisaSatisfacao` (os `Include` de `ProntuarioService.cs:172-178` não a incluem), então hoje toda consulta do histórico sai com `TemPesquisaRespondida = false`, mesmo quando a pesquisa existe. A projeção calcula o valor certo e, portanto, muda essa resposta. Nenhuma tela usa as consultas do histórico (as duas telas que chamam essa rota só exibem evoluções e documentos: `frontend/src/app/components/paciente/historico-paciente/historico-paciente.component.ts:20-87` e `frontend/src/app/components/medico/consultas-medico/consultas-medico.component.ts:100-130`), mas a diferença deve ser registrada como correção, e não aparecer como surpresa.

## 5. A regra de acesso clínico está escrita três vezes

`ObterHistoricoPacienteAsync` (`ProntuarioService.cs:163-310`, 148 linhas) faz quatro trabalhos em sequência: autorização por perfil (`165-203`), filtragem por instituição e consentimento (`205-259`), mapeamento para quatro tipos de DTO (`261-299`) e montagem (`301-309`). O médico é carregado duas vezes no mesmo método (`193` e `216`). A filtragem são dois `foreach` espelhados (`220-238` para consultas, `240-258` para documentos), cada um com um `if/else` cujo `else` repete o mesmo predicado de autorização ativa (`228-231` e `248-251`).

`ObterArquivoParaDownloadAsync` (`ProntuarioService.cs:386-439`) reimplementa a mesma regra contra o banco, com o predicado pela terceira vez (`415-419`). `ListarDocumentosAsync` (`380-384`) monta o histórico inteiro, com 7 cadeias de `Include`, para devolver só a lista de documentos. O teste "autorização ativa" (`RevogadaEmUtc == null`) aparece 6 vezes no arquivo.

### Proposta code-judo: uma função "pode ver dado desta instituição"

A regra cabe numa única função, calculada uma vez, e os dois `foreach` viram filtros:

```csharp
// depois das verificações de perfil já existentes (165-203), que não mudam;
// o médico passa a ser carregado uma única vez, antes do switch
Func<Guid, bool> podeVer = perfil switch
{
    PerfilUsuario.Paciente => _ => true,
    PerfilUsuario.Medico => origem => origem == medico.InstituicaoId
        || paciente.Autorizacoes.Any(a => a.RevogadaEmUtc == null
                                       && a.InstituicaoOrigemId == origem
                                       && a.InstituicaoDestinoId == medico.InstituicaoId),
    _ => _ => false // Enfermeiro: hoje recebe listas vazias, e continua recebendo
};

var consultasAutorizadas = paciente.Consultas.Where(c => podeVer(c.Slot.InstituicaoId)).ToList();
var documentosAutorizados = paciente.Documentos.Where(d => podeVer(d.InstituicaoOrigemId)).ToList();
```

As linhas `205-259` (55 linhas) viram 12, e o segundo carregamento do médico some. O download pode reutilizar a mesma regra extraindo-a para um método da classe (`PodeVerAsync(paciente, usuario, instituicaoOrigem)`). Um cuidado: uma propriedade calculada `Ativa => RevogadaEmUtc == null` na entidade funciona em memória, mas não é traduzível pelo EF; nas consultas ao banco (`415-419`) o predicado precisa continuar como expressão.

## 6. ProntuarioService faz trabalho de infraestrutura

O construtor de `ProntuarioService` recebe `IConfiguration` (`ProntuarioService.cs:25-32`) e executa `Directory.Exists`/`Directory.CreateDirectory` (`34-37`). Como o serviço é `Scoped` (`Program.cs:70`), isso roda em todo request que resolve o serviço, o que inclui qualquer ação de `PacientesController`, `ConsultasController`, `DocumentosController` e `AutorizacoesController`, porque todos o injetam. O serviço também grava e lê arquivos diretamente (`344-347`, `428-431`), valida upload com limites mágicos (5 MB em `328`, extensões em `333`) e, quando o arquivo não existe no disco, fabrica bytes de demonstração (`434-436`). Esse último ramo existe porque o seed registra um documento com `ChaveArquivo = "seed_ecg_carlos.pdf"` (`DbInitializer.cs:321`) que nunca é gravado em disco; é dado de demonstração vazando para dentro de uma regra de domínio.

Remédio no espírito de não criar camadas à toa: uma classe concreta `ArmazenamentoArquivos` (sem interface, registrada como singleton, que recebe as opções de storage e cria o diretório uma vez) com `SalvarAsync` e `LerAsync`. O seed passa a gravar o seu arquivo fictício com o mesmo conteúdo de texto que hoje é fabricado, e o ramo especial sai do serviço. Para o documento do seed, o download continua devolvendo os mesmos bytes; para um arquivo apagado manualmente do disco, o comportamento muda (passa a ser "não encontrado"), e isso deve ser registrado.

## 7. Autenticação e administração repetem o mesmo bloco três vezes

`AuthService.LoginAsync` (`AuthService.cs:41-66`) e `ObterUsuarioAtualAsync` (`80-103`) têm o mesmo bloco: decidir por perfil se busca `Paciente` ou `Medico` e montar `UsuarioInfoDto`. `AdminService.ListarUsuariosAsync` (`AuxiliaryServices.cs:381-410`) tem a terceira cópia, dentro de um `foreach`, com até uma consulta extra por usuário (N+1). `AuthService.cs:44`, `49`, `83` e `88` usam o nome qualificado `Models.Enums.PerfilUsuario` porque falta um `using`.

Uma única consulta com junções à esquerda devolve o DTO para todos os casos:

```csharp
IQueryable<UsuarioInfoDto> UsuariosInfo() =>
    from u in _context.Users
    join p in _context.Pacientes on u.Id equals p.UsuarioId into ps from p in ps.DefaultIfEmpty()
    join m in _context.Medicos on u.Id equals m.UsuarioId into ms from m in ms.DefaultIfEmpty()
    select new UsuarioInfoDto(u.Id, u.NomeCompleto, u.Email ?? "", u.Perfil, u.InstituicaoId,
        u.Instituicao != null ? u.Instituicao.Nome : null,
        u.Perfil == PerfilUsuario.Paciente && p != null ? p.Id : (Guid?)null,
        u.Perfil == PerfilUsuario.Medico && m != null ? m.Id : (Guid?)null);
```

`Login` usa `UsuariosInfo().FirstOrDefaultAsync(x => x.Id == usuario.Id)`, `Me` usa o mesmo filtro e `ListarUsuarios` usa a consulta sem filtro. O N+1 desaparece junto com as três cópias.

## 8. N+1 e orquestração sequencial

`ListarFilaEnfermeiroAsync` (`TriagemChatService.cs:374-403`) busca as coletas pendentes e, num `foreach` (`384-400`), faz uma ida ao MongoDB por coleta. Uma consulta com `$in` resolve:

```csharp
var ids = coletas.Select(c => c.ConversaId.ToString()).ToList();
var resumos = (await _mongoContext.Conversas.Find(c => ids.Contains(c.Id)).ToListAsync())
    .ToDictionary(c => c.Id, c => c.ResumoPreliminar.GerarTextoFormatado());
return coletas.Select(c => new ColetaItemFilaDto(c.Id, c.PacienteId, c.Paciente.Nome, c.InstituicaoId,
    c.Instituicao.Nome, c.Status, c.CriadaEmUtc, resumos.GetValueOrDefault(c.ConversaId.ToString()))).ToList();
```

`DashboardService.ObterDashboardOperacionalAsync` (`AuxiliaryServices.cs:139-173`) faz até 7 consultas sequenciais (`152-161`): quatro `CountAsync` sobre a mesma consulta, só mudando o status, mais coletas, total de pesquisas e média. O `DbContext` não permite paralelizar essas chamadas; o remédio é colapsar as quatro contagens num único `GroupBy(c => c.Status)`.

## 9. Escritas em várias etapas sem fronteira explícita

Vários fluxos gravam em dois lugares em sequência, sem que a ordem e a política de falha estejam escritas em um ponto único: `IniciarColetaAsync` grava no PostgreSQL (`TriagemChatService.cs:55`) e depois insere no MongoDB (`84`); `FinalizarEnvioEnfermeiroAsync` (`355` e `369`) e `ValidarColetaAsync` (`429` e `453`) fazem o mesmo. `CriarPacienteAsync` faz três gravações independentes (`AuxiliaryServices.cs:293`, `300`, `314`), e `CriarUsuarioAsync` também (`427`, `434`, `453`). `AnexarDocumentoAsync` grava o arquivo (`ProntuarioService.cs:344-347`) antes de gravar o registro (`363`).

A especificação aceita a ausência de transação distribuída, mas exige "criar primeiro coleta pendente no PostgreSQL; inicializar documento MongoDB de forma idempotente por coletaId" (`prompt-poc-clinica-instrucoes-agent.md`, seção 6). O comentário de `TriagemChatService.cs:57` diz "de forma idempotente", mas `InsertOneAsync` com um id novo não é idempotente. Do ponto de vista estrutural, o passo desta refatoração é concentrar a gravação da conversa num método único, para que a política fique num lugar só. Como `UserManager` usa o mesmo `AppDbContext` com escopo de request, as gravações de Identity e EF podem ser envolvidas por uma única `BeginTransactionAsync`. Essas mudanças só alteram o que acontece quando há falha no meio do caminho, por isso a lista de bugs do resumo registra os efeitos atuais.

## 10. Organização, abstrações finas e modernização pontual

`AuxiliaryServices.cs` reúne cinco serviços sem relação entre si (notificações e pesquisa, dashboard, instituições, pacientes, administração). O nome não diz o que o arquivo contém. Um arquivo por serviço, agrupado por módulo junto com o controller correspondente, resolve.

Os 10 serviços têm, cada um, uma interface com uma única implementação, declarada no mesmo arquivo (`Program.cs:66-75` registra os 10 pares). Não há testes nem implementações alternativas (0 arquivos de teste em `main`). As interfaces são indireção sem ganho hoje. Removê-las é opcional e de baixa prioridade; se o plano de testes da disciplina for usar mocks, elas podem ficar.

Itens de modernização encontrados nos serviços:

- `foreach` com `List.Add` onde cabe projeção: `TriagemChatService.cs:383-402` e `AuxiliaryServices.cs:381-410` (os dois também são N+1).
- `FromSqlRaw("... WHERE \"Id\" = {0} FOR UPDATE", dto.PacienteId)` em `AgendaService.cs:121` pode virar `FromSql($"... WHERE \"Id\" = {dto.PacienteId} FOR UPDATE")`, a API interpolada do EF Core 7 ou posterior, que gera o mesmo parâmetro.
- `.Include(c => c.Slot)` dentro de `AnyAsync` em `AgendaService.cs:167` não tem efeito e pode sair.
- 10 chamadas `AddAsync`/`AddRangeAsync`; a documentação do EF recomenda `Add`, porque a versão assíncrona só importa para geradores de valor especiais.
- 11 atribuições `Id = Guid.NewGuid()` em entidades cujo inicializador já faz isso (`Entities.cs:33`, `54`, `84`, `110`, `132`, `165`, `196`, `217`, `251`, `276`, `301`), mais 15 `Id = Guid.NewGuid().ToString()` e 15 `EnviadaEmUtc = DateTime.UtcNow` em mensagens que já têm esses padrões.
- 36 leituras de `DateTime.UtcNow` nos serviços. O `TimeProvider` do .NET 8 torna regras como "não agendar no passado" (`AgendaService.cs:151`) testáveis sem relógio real.
- Construtores primários do C# 12 eliminariam cerca de seis linhas de atribuição por serviço; é opcional.
- Os 12 DTOs de entrada são classes mutáveis com 35 propriedades `{ get; set; }` (`DTOs/DomainDtos.cs`, por exemplo `13-20` e `34-39`), enquanto os de saída já são `record`. Convertê-los em `record` é seguro desde que se use `{ get; init; } = string.Empty` e não records posicionais: num record posicional, um campo de texto ausente no JSON chega como `null` em vez de `""`, e `EnviarMensagemChatDto.Conteudo` é usado com `.Trim()` em `TriagemChatService.cs:143`.
- O template do resumo existe em `ConversaMongo.cs:46`, reescrito à mão no seed (`DbInitializer.cs:239`) e no frontend (`frontend/src/app/components/enfermeiro/fila-enfermeiro/fila-enfermeiro.component.ts:163`), embora o item da fila já traga `resumoPreliminarTexto` pronto.

## Apêndice: script de medição de métodos

Heurística: localiza assinaturas de método C# por expressão regular e conta as linhas até a chave que fecha o corpo. Ignora `bin/`, `obj/` e `Migrations/`.

```python
import re, sys, pathlib
# Heurística: assinatura de método C# (modificador + '(') seguida de '{' na mesma linha ou na seguinte; conta as linhas até a chave correspondente.
sig = re.compile(r'^\s*(public|private|protected|internal)\s+(static\s+)?(async\s+)?[\w<>\[\]\?,\s\(\)]+\s+(\w+)\s*\(')
root = pathlib.Path(sys.argv[1]); limite = int(sys.argv[2])
for f in sorted(root.rglob('*.cs')):
    if any(p in f.parts for p in ('bin','obj','Migrations')): continue
    lines = f.read_text(encoding='utf-8-sig').splitlines()
    i = 0
    while i < len(lines):
        m = sig.match(lines[i])
        if m and not lines[i].rstrip().endswith(';') and '=>' not in lines[i] and ' class ' not in lines[i] and ' interface ' not in lines[i]:
            j = i
            while j < len(lines) and '{' not in lines[j] and not lines[j].rstrip().endswith(';'):
                j += 1
            if j < len(lines) and '{' in lines[j]:
                depth = 0; k = j
                while k < len(lines):
                    depth += lines[k].count('{') - lines[k].count('}')
                    if depth == 0: break
                    k += 1
                n = k - i + 1
                if n >= limite:
                    print(f"{n:4d} linhas  {f.relative_to(root)}:{i+1}-{k+1}  {m.group(4)}")
                i = k
        i += 1
```
