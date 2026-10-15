# 04. Frontend Angular

Linha de base: commit `8ff4b1d` (branch `main`). Caminhos relativos a `frontend/src/app/`, salvo indicação.

Versão real do framework: `frontend/package.json:13-20` declara `@angular/*` em `^19.0.0`, e `frontend/package-lock.json:551-552` trava `@angular/core` em `19.2.25`. O projeto é Angular 19, e não Angular 20 nem Angular 16 (este último é o que dizem `.ai/tech-stack.md:14` e o `README.md:47`). As recomendações abaixo usam apenas recursos estáveis no Angular 19.

## 1. Medições

Distribuição de linhas dos 14 componentes (script do apêndice, que localiza os blocos `template:` e `styles:` de cada arquivo):

| Componente | Total | Template | Estilos | Classe e imports |
| --- | ---: | ---: | ---: | ---: |
| `paciente/chat-triagem` | 365 | 105 | 160 | 100 |
| `login` | 258 | 104 | 85 | 69 |
| `medico/consultas-medico` | 253 | 122 | 19 | 112 |
| `paciente/agenda-paciente` | 241 | 102 | 39 | 100 |
| `enfermeiro/fila-enfermeiro` | 191 | 80 | 44 | 67 |
| `paciente/historico-paciente` | 171 | 79 | 11 | 81 |
| `navbar` | 156 | 59 | 69 | 28 |
| `paciente/pesquisa-satisfacao` | 151 | 75 | 17 | 59 |
| `atendente/dashboard-atendente` | 149 | 84 | 23 | 42 |
| `paciente/autorizacoes-paciente` | 148 | 70 | 7 | 71 |
| `paciente/consultas-paciente` | 120 | 56 | 14 | 50 |
| `paciente/notificacoes-paciente` | 110 | 52 | 17 | 41 |
| `admin/admin-usuarios` | 83 | 34 | 7 | 42 |
| `admin/admin-saude` | 70 | 35 | 5 | 30 |
| **Total** | 2.467 | 1.057 | 517 | 892 |

Os demais arquivos: `services/api.service.ts` (317 linhas, das quais as linhas 5-167 são interfaces), `services/auth.service.ts` (69), `interceptors/auth.interceptor.ts` (19), `app.routes.ts` (46), `app.component.ts` (25) e `app.component.html` (336, não usado; ver seção 8). Nenhum arquivo passa de 400 linhas e nenhum método passa de 30 linhas. O problema do frontend não é tamanho, é repetição.

Comandos (executados em `frontend/src/app/`):

```bash
grep -rn "\.subscribe(" . | wc -l                                   # 38
grep -rn "alert(err.error?.mensagem" . | wc -l                     # 11
grep -rn "error: () => this.loading = false" . | wc -l             # 10
grep -rn "err.error?.mensagem" . | wc -l                           # 13
grep -rn "subscribe(" components | grep -v "subscribe({"            # 13 chamadas de API sem callback de erro
grep -rnoE ":\s*any\b|<any" . | wc -l                              # 11
grep -rn "localhost:5000" .                                         # api.service.ts:173, auth.service.ts:27
grep -rn "getStatusBadge(status\|getRoleBadge(perfil\|getRoleBadgeClass(perfil" .
grep -rn "if (!user?.pacienteId" . | wc -l                         # 5
grep -rhoE "\*ng(If|For|SwitchCase)|\[ngSwitch\]" components app.component.ts | sort | uniq -c
grep -rl "CommonModule" . | wc -l                                   # 15
grep -rn "standalone: true" . | wc -l                               # 15
grep -rhoE "date:'[^']+'" components | sort | uniq -c
grep -rn "canActivate\|CanActivate" . | wc -l                       # 0
grep -rn "HttpParams" . | wc -l                                     # 0
for p in "\.modal-overlay {" "\.empty-card {" "\.page-header { margin-bottom: 1.5rem; }" "\.data-table {"; do
  echo "$p $(grep -rln "$p" components | wc -l)"; done              # 3, 7, 10, 2
```

## 2. O mesmo bloco de carregamento e erro, escrito em cada componente

Quase toda chamada de API segue a mesma forma: ligar uma flag, assinar, desligar a flag em `next` e em `error`, e mostrar `err.error?.mensagem` com um texto padrão. O componente de chat tem três cópias seguidas (`components/paciente/chat-triagem/chat-triagem.component.ts:292-304`, `318-331` e `333-346`):

```ts
this.loading = true;
this.api.finalizarColeta(this.conversa.coletaId).subscribe({
  next: (res) => { this.conversa = res; this.loading = false; },
  error: (err) => {
    this.loading = false;
    alert(err.error?.mensagem || 'Erro ao finalizar coleta.');
  }
});
```

No total são 38 assinaturas: 11 terminam em `alert(err.error?.mensagem || ...)`, 10 engolem o erro com `error: () => this.loading = false`, e 13 chamadas de API não têm callback de erro nenhum (por exemplo `components/paciente/consultas-paciente/consultas-paciente.component.ts:106`, `components/medico/consultas-medico/consultas-medico.component.ts:183`). Os erros engolidos escondem defeitos reais: o link "Pacientes" do atendente (`app.routes.ts:39`) abre `AdminUsuariosComponent`, que chama uma rota exclusiva do admin; o 403 é descartado em `components/admin/admin-usuarios/admin-usuarios.component.ts:69` e a tela mostra uma tabela vazia.

Não há tratamento central de erro HTTP. `interceptors/auth.interceptor.ts` só acrescenta o cabeçalho. O token expira em 24 horas, mas `expiraEmUtc` não é guardado e `AuthService.isAuthenticated()` (`services/auth.service.ts:56-58`) só verifica se existe algo no `localStorage`; depois da expiração, a interface continua "logada" e todas as chamadas falham com 401, a maioria em silêncio.

### Proposta code-judo

O passo que preserva todos os textos atuais é um auxiliar de uma linha e o operador `finalize`:

```ts
// shared/erros.ts
export const mensagemDe = (err: HttpErrorResponse, padrao: string) => err.error?.mensagem || padrao;

// uso
this.loading = true;
this.api.finalizarColeta(id).pipe(finalize(() => (this.loading = false))).subscribe({
  next: res => (this.conversa = res),
  error: err => alert(mensagemDe(err, 'Erro ao finalizar coleta.'))
});
```

O operador `||` foi mantido de propósito, para que uma mensagem vazia continue caindo no texto padrão. Numa segunda etapa, um interceptor de erro trata o 401 num só lugar (`auth.logout()`); isso é comportamento novo e deve ser registrado como correção.

## 3. Regras por perfil espalhadas em quatro lugares, e nenhuma guarda de rota

O que cada perfil vê e para onde vai está escrito separadamente em:

- `components/login/login.component.ts:237-257`: `switch` com a rota inicial de cada perfil.
- `components/navbar/navbar.component.ts:21-50`: `ngSwitch` com os links de cada perfil.
- `components/navbar/navbar.component.ts:146-155`: `switch` com a cor do selo de cada perfil.
- `components/admin/admin-usuarios/admin-usuarios.component.ts:73-82`: cópia do mesmo `switch` de cores.
- `app.routes.ts:20-43`: as rotas, separadas por prefixo de perfil, sem `canActivate` (0 ocorrências no projeto).

Os três `switch` recebem `perfil: string`, embora `UsuarioInfo['perfil']` já seja uma união tipada (`services/auth.service.ts:10`). Sem guardas, qualquer pessoa navega para `/admin/usuarios`; o backend bloqueia os dados, e a tela renderiza vazia.

### Proposta code-judo: uma tabela de perfis

```ts
// perfis.ts
export type Perfil = UsuarioInfo['perfil'];

export const PERFIS = {
  Paciente:   { inicio: '/paciente/triagem',    selo: 'badge-primary', links: [/* 6 links */] },
  Enfermeiro: { inicio: '/enfermeiro/fila',     selo: 'badge-info',    links: [/* 1 link  */] },
  Medico:     { inicio: '/medico/consultas',    selo: 'badge-success', links: [/* 2 links */] },
  Atendente:  { inicio: '/atendente/dashboard', selo: 'badge-warning', links: [/* 3 links */] },
  Admin:      { inicio: '/admin/usuarios',      selo: 'badge-danger',  links: [/* 2 links */] },
} satisfies Record<Perfil, { inicio: string; selo: string; links: { rota: string; rotulo: string }[] }>;

export const perfilGuard: CanActivateFn = rota => {
  const perfil = inject(AuthService).currentUser()?.perfil;
  return (perfil && rota.data['perfis'].includes(perfil)) || inject(Router).parseUrl('/login');
};
```

O login passa a fazer `router.navigate([PERFIS[perfil].inicio])`, a navbar renderiza `PERFIS[user.perfil].links` com um único laço, e as rotas declaram `canActivate: [perfilGuard], data: { perfis: ['Paciente'] }`. Os três `switch` desaparecem. As guardas são comportamento novo (redirecionam em vez de mostrar tela vazia) e devem ser registradas como tal.

## 4. Selos de status copiados

`getStatusBadge` existe três vezes, idêntico, em `components/paciente/consultas-paciente/consultas-paciente.component.ts:111-119`, `components/medico/consultas-medico/consultas-medico.component.ts:244-252` e `components/atendente/dashboard-atendente/dashboard-atendente.component.ts:140-148`. Um mapa tipado substitui as três cópias e acompanha a união de status que já existe em `services/api.service.ts:48`:

```ts
export const SELO_STATUS: Record<Consulta['status'], string> = {
  Agendada: 'badge-primary', EmAtendimento: 'badge-warning', Concluida: 'badge-success', Cancelada: 'badge-danger'
};
// template: [ngClass]="SELO_STATUS[c.status] ?? 'badge-secondary'"
```

## 5. Contrato da API com tipos frouxos

Há 11 usos de `any`. Sete estão no `ApiService`: `validarColeta` (`services/api.service.ts:212`), `atualizarStatusConsulta` (`248`), `revogarAutorizacao` (`287`), `getUsuariosAdmin` (`310-311`) e `getHealth` (`314-315`). Os outros quatro estão nos componentes: `chat-triagem.component.ts:348` (`msg: any`), `historico-paciente.component.ts:136` (`event: any`), `admin-usuarios.component.ts:55` e `admin-saude.component.ts:53`.

Dois desses tipos já existem. A rota `GET /api/admin/usuarios` devolve `UsuarioInfoDto`, que tem a mesma forma de `UsuarioInfo` (`services/auth.service.ts:6-15`), então `getUsuariosAdmin(): Observable<UsuarioInfo[]>` resolve sem criar nada. A mensagem do chat é um tipo anônimo dentro de `ConversaDetalhe` (`services/api.service.ts:71-78`); dar nome a ele (`MensagemChat`) tira o `any` de `isUltimaMensagem`. Para a saúde dos serviços, basta uma interface de três campos. As respostas `{ mensagem }` das rotas de ação podem ser tipadas como `Observable<{ mensagem: string }>`.

Campos que o backend envia como enum em texto (por causa do `JsonStringEnumConverter`, `backend/Consultorio.Api/Program.cs:93`) estão tipados como `string` em `PesquisaSatisfacao.percepcao` (`services/api.service.ts:154`), `ColetaItemFila.status` (`88`) e `Notificacao.finalidade` (`142`). `atualizarStatusConsulta(id, novoStatus: string)` aceita qualquer texto. A união já existe para `Consulta.status`; os outros merecem o mesmo tratamento.

## 6. Endereço da API e parâmetros de consulta

O endereço `http://localhost:5000/api` está fixo em dois serviços (`services/api.service.ts:173` e `services/auth.service.ts:27`). Não há `environment.ts`. O `README.md:42` manda rodar `ng serve --proxy-config proxy.conf.json`, mas o arquivo não existe, e o perfil de execução do backend usa a porta 5240 (`backend/Consultorio.Api/Properties/launchSettings.json:17`). A solução que já está descrita no README e apaga as duas constantes é usar a URL relativa `/api` e criar o `proxy.conf.json` apontando para a porta da API. Isso também tira a dependência de CORS no desenvolvimento. Se a equipe preferir não usar proxy, uma única constante exportada resolve a duplicação.

As consultas com parâmetros são montadas à mão em `services/api.service.ts:183`, `218-222`, `236-241` e `268`, sem codificação de URL. O `HttpClient` aceita um objeto em `params`:

```ts
const semVazios = (o: Record<string, string | undefined>) =>
  Object.fromEntries(Object.entries(o).filter(([, v]) => !!v)) as Record<string, string>;

getSlots(medicoId?: string, instituicaoId?: string, data?: string) {
  return this.http.get<SlotAgenda[]>(`${API}/slots`, { params: semVazios({ medicoId, instituicaoId, data }) });
}
```

`getFilaEnfermeiro` envia `?status=PendenteValidacao` (`services/api.service.ts:209`), parâmetro que o backend ignora (ver `01_backend_controllers_e_erros.md`, seção 5).

## 7. Padrões de Angular anteriores à versão instalada

O projeto está no Angular 19, mas usa a sintaxe de antes da versão 17:

- 69 `*ngIf`, 22 `*ngFor`, 5 `*ngSwitchCase` e 1 `[ngSwitch]`. O controle de fluxo nativo (`@if`, `@for`, `@switch`) é estável desde a versão 17, e o schematic `ng generate @angular/core:control-flow` faz a conversão automaticamente. O `@for` exige `track`, o que obriga a escolher uma chave para cada lista (o `id` existe em todas).
- 15 arquivos importam `CommonModule` inteiro. Depois da conversão, cada componente importa só o que usa (`DatePipe`, `DecimalPipe`, `NgClass`).
- 15 declarações `standalone: true`, que é o padrão a partir do Angular 19 e pode sair.
- O estado é guardado em campos comuns, alterados dentro de callbacks. `getNotificacoesFiltradas()` é chamado três vezes no template a cada ciclo de detecção de mudanças (`components/paciente/notificacoes-paciente/notificacoes-paciente.component.ts:36`, `40` e `41`); um `computed` sobre um `signal` resolve. O `AuthService` já usa `signal` para o usuário atual, então o padrão existe no projeto.
- Os dois serviços usam injeção pelo construtor (`services/api.service.ts:175`, `services/auth.service.ts:33`), enquanto os componentes usam `inject()`. Vale padronizar em `inject()`.
- `concluirAtendimento` tem uma assinatura dentro de outra (`components/medico/consultas-medico/consultas-medico.component.ts:205-223`), com o tratamento de erro duplicado nos dois níveis. `switchMap` encadeia as duas chamadas com um único tratamento de erro.

## 8. Código morto

- `app.component.html` (336 linhas) é o placeholder gerado pelo Angular CLI, com o aviso "Delete the template below" no início, e `app.component.css` está vazio. Nenhum dos dois é referenciado, porque `app.component.ts:11-21` usa template e estilos inline.
- `AuthService` é injetado e nunca usado em `chat-triagem.component.ts:280`, `consultas-paciente.component.ts:84` e `consultas-medico.component.ts:155`. `Router` é injetado e nunca usado em `agenda-paciente.component.ts:158`. `Instituicao` é importado e nunca usado em `admin-usuarios.component.ts:4`.
- A referência de template `#scrollContainer` (`chat-triagem.component.ts:29`) nunca é lida.
- `ApiService.criarSlot` (`services/api.service.ts:226-228`) não tem chamador. A rota `POST /api/slots` existe no backend, mas não há tela que a use.

## 9. Duplicação que atravessa a fronteira com o backend

- `components/enfermeiro/fila-enfermeiro/fila-enfermeiro.component.ts:163` reescreve, em TypeScript, o template do resumo que o backend gera em `backend/Consultorio.Api/Models/Mongo/ConversaMongo.cs:46`. O item da fila já traz esse texto pronto (`ColetaItemFila.resumoPreliminarTexto`, `services/api.service.ts:90`), gerado pela mesma função no backend; usar esse campo dá o mesmo texto e elimina a cópia.
- O número da etapa final do roteiro (9) está no template do chat (`chat-triagem.component.ts:70` e `103`), copiado da máquina de estados do backend. Uma constante exportada, ou um campo booleano calculado no DTO, evita que as duas pontas divirjam; a segunda opção muda o contrato e deve ser deixada para depois.
- O código de download (criar URL de objeto, clicar num link e revogar a URL) está duplicado em `components/paciente/historico-paciente/historico-paciente.component.ts:161-170` e `components/medico/consultas-medico/consultas-medico.component.ts:233-242`. Pode virar um único método do `ApiService`.
- O formato de data `dd/MM/yyyy HH:mm` aparece 10 vezes nos templates. O token `DATE_PIPE_DEFAULT_OPTIONS` define o formato padrão uma vez. A especificação pede conversão para `America/Sao_Paulo` na interface; hoje os templates usam o fuso do navegador. Fixar o fuso no mesmo token muda o que aparece para quem estiver fora desse fuso, então é uma decisão separada.
- `getInstituicoes()` é chamado em quatro componentes (`chat-triagem:289`, `agenda-paciente:176`, `historico-paciente:116`, `autorizacoes-paciente:101`). É uma lista estática na POC; `shareReplay(1)` no serviço evita as requisições repetidas, mas o ganho é pequeno.

## 10. CSS repetido entre componentes

Os estilos inline somam 517 linhas. Blocos idênticos aparecem em vários componentes: `.page-header { margin-bottom: 1.5rem; }` em 10, `.empty-card` em 7, `.modal-overlay` em 3, `.data-table`, `.table-card` e `.btn-close` em 2 cada. O `styles.css` global já é o lugar das classes utilitárias (`src/styles.css:33-166`), e esses blocos devem ir para lá. As 160 linhas de estilo do chat (`chat-triagem.component.ts:117-276`) são específicas do visual de WhatsApp e podem ficar no componente, de preferência num arquivo `styleUrl` próprio. Os quatro templates com mais de 100 linhas (`consultas-medico`, `chat-triagem`, `login` e `agenda-paciente`) também ficariam mais legíveis em arquivo próprio; é opcional.

## 11. Componentes reaproveitados fora do seu papel

`AgendaPacienteComponent` atende três rotas (`app.routes.ts:22`, `34` e `38`), mas só implementa o fluxo do paciente: `confirmarAgendamento` sai em silêncio quando o usuário não tem `pacienteId` (`components/paciente/agenda-paciente/agenda-paciente.component.ts:213`). Para médico e atendente, a tela mostra um botão "Reservar Horário" que não faz nada. `AdminUsuariosComponent` atende `/atendente/pacientes` (`app.routes.ts:39`) chamando uma rota que só o admin pode usar. São defeitos funcionais (estão na lista do resumo). Do ponto de vista estrutural, o componente deve ter o nome do que faz, e a tabela de perfis da seção 3 é o lugar natural para decidir qual tela cada perfil recebe.

## Apêndice: script da tabela da seção 1

Executado em `frontend/src/app/`.

```python
import pathlib
for f in sorted(pathlib.Path('components').rglob('*.ts')):
    L = f.read_text().splitlines()
    t0 = next(i for i, l in enumerate(L) if 'template: `' in l)
    t1 = next(i for i in range(t0 + 1, len(L)) if L[i].strip().startswith('`,'))
    s0 = next(i for i, l in enumerate(L) if 'styles: [`' in l)
    s1 = next(i for i in range(s0 + 1, len(L)) if L[i].strip().startswith('`]'))
    tpl, sty = t1 - t0 + 1, s1 - s0 + 1
    print(f, len(L), tpl, sty, len(L) - tpl - sty)
```
