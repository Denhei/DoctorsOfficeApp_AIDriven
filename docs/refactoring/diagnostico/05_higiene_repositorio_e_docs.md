# 05. Higiene do repositório e documentação

Linha de base: commit `8ff4b1d` (branch `main`). Caminhos relativos à raiz do repositório.

## 1. Artefatos de build versionados

Em `main`, o índice do Git contém 141 arquivos gerados pelo build do backend, 111 em `backend/Consultorio.Api/bin/` e 30 em `backend/Consultorio.Api/obj/`, somando 37,0 MB. São 111 binários (`.exe`, `.dll`, `.pdb`), entre eles `Consultorio.Api.exe` e `obj/Debug/net8.0/apphost.exe`, ou seja, saída de um build feito no Windows. O arquivo `obj/Debug/net8.0/Consultorio.Api.csproj.FileListAbsolute.txt` grava caminhos absolutos da máquina de quem compilou (`D:\mba\Consultorio\backend\...`). O total de arquivos versionados em `main` é 215; os artefatos são 66% deles.

```bash
git ls-tree -r -l main | grep -E '/(bin|obj)/' | awk '{n++; s+=$4} END {printf "%d arquivos, %.1f MB\n", n, s/1048576}'
# 141 arquivos, 37.0 MB
git ls-tree -r --name-only main | grep -cE '/bin/'      # 111
git ls-tree -r --name-only main | grep -cE '/obj/'      # 30
git ls-tree -r -l main | grep -cE '\.(exe|dll|pdb)$'    # 111
git ls-tree -r --name-only main | wc -l                 # 215
git show main:backend/Consultorio.Api/obj/Debug/net8.0/Consultorio.Api.csproj.FileListAbsolute.txt | head -1
# D:\mba\Consultorio\backend\Consultorio.Api\bin\Debug\net8.0\appsettings.Development.json
```

Não havia `.gitignore` para o backend em `main`; o único era `frontend/.gitignore`, que só cobre o frontend. A consequência prática apareceu no início desta auditoria: um build local em macOS já tinha deixado arquivos não rastreados em `bin/` e `obj/` (`git status` listava `bin/Debug/net8.0/Consultorio.Api` e `obj/Debug/net8.0/Consultorio.Api.csproj.CopyComplete`), e qualquer build alteraria arquivos rastreados.

Situação durante a auditoria: o commit `415c865`, feito na branch `refactoring` enquanto este diagnóstico era escrito, já removeu os 141 arquivos do índice, apagou o `Consultorio.Api.http` (seção 2) e criou `backend/.gitignore` com `bin/`, `obj/`, `uploads/`, `TestResults/` e `*.user`. Restam três pontos:

- Não existe `.gitignore` na raiz. Um `.env` criado a partir de `.env.example` na raiz, que é o uso esperado desse arquivo, não seria ignorado.
- A pasta de upload é relativa ao diretório de execução (`Storage:UploadDirectory = "./uploads"`, `backend/Consultorio.Api/appsettings.json:22`, usada em `backend/Consultorio.Api/Services/ProntuarioService.cs:32`). Se a API for iniciada a partir da raiz do repositório, a pasta nasce na raiz, fora do alcance de `backend/.gitignore`.
- Remover do índice não reduz o histórico: os 37 MB continuam no Git. Reescrever o histórico é opcional e não faz parte desta refatoração.

## 2. Restos de template

- `backend/Consultorio.Api/Consultorio.Api.http` (6 linhas) apontava para `GET /weatherforecast/` na porta 5240, uma rota que não existe no projeto. Foi removido em `415c865`.
- `frontend/src/app/app.component.html` (336 linhas) é o placeholder do Angular CLI e `frontend/src/app/app.component.css` está vazio. Nenhum dos dois é usado (ver `04_frontend.md`, seção 8).
- `frontend/README.md` é o texto padrão do Angular CLI 19.0.2.
- `frontend/src/index.html:2` declara `lang="en"` e `frontend/src/index.html:5` usa o título `Frontend`, numa aplicação inteiramente em português.
- `backend/Consultorio.Api/Properties/launchSettings.json:32-39` mantém um perfil "IIS Express".
- `backend/Consultorio.Api/Consultorio.Api.csproj:13` referencia `Microsoft.AspNetCore.OpenApi` sem uso, e `csproj:10` referencia `BCrypt.Net-Next` sem uso (ver `03_backend_config_dados_seed.md`, seção 7).

## 3. Documentação que descreve outro sistema

Os arquivos `.ai/*.md` e o `README.md` são o contexto que um agente de IA lê antes de trabalhar no projeto. Quando eles descrevem versões e regras que o código não tem, o agente produz código para o sistema errado. Esta é a razão para tratar a divergência como dívida técnica, e não como detalhe de documentação.

| Documento | O que afirma | O que o código mostra |
| --- | --- | --- |
| `README.md:20` | ".NET 6 SDK ou superior" | `backend/Consultorio.Api/Consultorio.Api.csproj:4` exige `net8.0` |
| `README.md:30-35` | API em `http://localhost:5000`, teste com `curl http://localhost:5000/health` | o perfil padrão usa 5240 (`launchSettings.json:17`) e a rota é `/api/health` (`HealthController.cs:9`) |
| `README.md:42` | `ng serve --proxy-config proxy.conf.json` | o arquivo não existe; o frontend chama `http://localhost:5000` direto |
| `README.md:47-50` | Angular 16, ASP.NET Core 7, PostgreSQL 15, MongoDB 6 | Angular 19.2.25 (`package-lock.json:552`), `net8.0`, `postgres:16-alpine` e `mongo:7.0` (`docker-compose.yml:5` e `23`) |
| `README.md:52` | CI em `.github` | não existe |
| `README.md:59` | `plano_implementacao_poc.md` | não existe; o plano está em `Context_Prompts/Implementation_Plan.pdf` |
| `.ai/architecture.md:6` e `.ai/tech-stack.md:4-6` | ASP.NET Core 7, EF Core 7 | pacotes 8.0.11 (`csproj:11-19`) |
| `.ai/architecture.md:18` e `.ai/tech-stack.md:14-16` | Angular 16 com Angular Material | Angular 19, sem Angular Material em `package.json` |
| `.ai/tech-stack.md:10` | AutoMapper (opcional) | não usado; 24 mapeamentos manuais (`02_backend_services.md`) |
| `.ai/tech-stack.md:11` e `.ai/standards.md:18-19` | testes xUnit e `*.spec.ts`, cobertura de 80% | 0 arquivos de teste em `main` |
| `.ai/tech-stack.md:22-23` | PostgreSQL 15, MongoDB 6 | `postgres:16-alpine`, `mongo:7.0` |
| `.ai/business-rules.md:4` | a sequência de perguntas fica armazenada no MongoDB | o roteiro está fixo em C# (`TriagemChatService.cs:157-323`); o MongoDB guarda só a conversa |
| `.ai/business-rules.md:9` | rota `/api/enfermeiro/validar` | a rota real é `PUT /api/coletas/{id}/validacao` (`DomainControllers.cs:284`) |
| `.ai/business-rules.md:10` | na rejeição, o paciente retoma a triagem | `EncaminhadaPresencial` é estado final (`TriagemChatService.cs:426`) e o chat só aceita mensagens em `EmColeta` (`TriagemChatService.cs:126-129`) |
| `.ai/business-rules.md:13-14` | campo `Disponivel` em `SlotAgenda`, alterado na reserva | o campo não existe (`Entities.cs:107-127`); a disponibilidade é calculada (`AgendaService.cs:64`) |
| `.ai/business-rules.md:19` | evolução finalizada quando todos os campos obrigatórios estiverem preenchidos | a evolução tem um único campo de texto (`Entities.cs:208-209`) |
| `.ai/business-rules.md:22-23` | notificação ao mudar status da coleta, canal preferencial do paciente | só há notificação na conclusão da consulta, sempre nos dois canais (`ProntuarioService.cs:128-155`); `FinalidadeNotificacao.StatusColeta` nunca é usada |
| `.ai/business-rules.md:30` | administrador com acesso total | o admin é bloqueado no histórico clínico e no download (`ProntuarioService.cs:166-169` e `388-391`), como pede a especificação em `Context_Prompts/` |
| `.ai/standards.md:5` | interfaces TypeScript com prefixo `I` | 0 das 15 interfaces usam prefixo |
| `.ai/standards.md:8` | indentação de 4 espaços | o frontend usa 2 (`frontend/.editorconfig:7`); o backend usa 4 |
| `.ai/standards.md:10` | comentários XML em membros públicos | 3 `/// <summary>` nos controllers, 0 nos serviços |
| `.ai/standards.md:11` | lint com `npm run lint` | `frontend/package.json` não tem script `lint` |
| `.ai/standards.md:14-15` | Conventional Commits e branches `feature/` | o histórico de `main` tem "Project Upload" e "Uploading README" |

Na maioria das linhas, quem está errado é o documento: versões, rota de validação, campo `Disponivel` e acesso do administrador. Em dois casos a escolha é de padrão, não de fato: o prefixo `I` (o guia de estilo do Angular desaconselha, então o mais simples é retirar a regra) e a indentação (definir por linguagem). As regras de notificação por status e retomada da triagem descrevem funcionalidades que não existem; para uma refatoração, o correto é documentar o comportamento atual e registrar as lacunas, e não implementá-las.

## 4. O que falta para refatorar com segurança

Em `main`, o repositório não tem testes (0 arquivos `*.spec.ts` ou projetos `*.Tests`), não tem lint configurado, não tem CI e não tem arquivo `.sln`. Uma refatoração "sem mudar comportamento" precisa de algo que prove o comportamento. O mínimo que resolve, sem montar infraestrutura de testes completa, é um roteiro de caracterização: um script (ou arquivo `.http`) que faz login com as contas do seed e chama as 36 rotas (33 de `DomainControllers.cs`, 2 de autenticação e o health check), gravando status e corpo de cada resposta. Ele roda contra `main` uma vez para gerar a referência e depois de cada etapa da refatoração; qualquer diferença precisa estar na lista de mudanças deliberadas. As correções conscientes listadas nos outros relatórios (por exemplo, as nove rotas que deixam de responder 500) aparecem como diferenças esperadas.

## 5. Segredos no repositório

A chave de assinatura JWT está em `backend/Consultorio.Api/appsettings.json:16`, e dois outros valores fixos estão no código (`Program.cs:38` e `Services/JwtTokenGenerator.cs:27`). Para uma POC acadêmica local isso é aceitável, mas os padrões no código fazem a aplicação subir com uma chave conhecida mesmo sem configuração. Depois da unificação em `JwtOptions` (`03_backend_config_dados_seed.md`, seção 2), o código não precisa de padrão: a chave vem da configuração (ou de `dotnet user-secrets` no desenvolvimento), e a ausência dela deve impedir a partida.
