# Consultório – Prova de Conceito (POC)

## Visão geral
Esta POC demonstra um fluxo completo de **triagem, validação, agendamento, consulta e pós‑consulta** para um sistema de consultório médico. O projeto está dividido em três partes principais:
- **Frontend** – SPA em Angular.
- **Backend** – API REST em ASP.NET Core.
- **Bancos de dados** – PostgreSQL (dados relacionais) e MongoDB (chat).

## Estrutura de pastas
```
Consultorio/
├─ backend/      # API ASP.NET Core
├─ frontend/     # Aplicação Angular
├─ .ai/          # Documentação de arquitetura, padrões, regras de negócio
└─ README.md     # Este documento
```

## Como executar localmente
### Pré‑requisitos
- .NET 6 SDK ou superior (`dotnet --version`).
- Node 20+ e npm (`node -v`).
- Angular CLI (`npm i -g @angular/cli`).
- PostgreSQL e MongoDB em execução (padrão nas portas 5432 e 27017).

### Backend
```powershell
cd backend/Consultorio.Api
dotnet restore
# Ajuste as strings de conexão em appsettings.Development.json se necessário
dotnet run --urls "http://localhost:5000"
```
A API ficará disponível em `http://localhost:5000`.  Teste rápido:
```powershell
curl http://localhost:5000/health
```
Deve retornar o status dos bancos.

### Frontend
```powershell
cd ../../frontend
npm ci
ng serve --proxy-config proxy.conf.json --open
```
O Angular abrirá o navegador em `http://localhost:4200` e utilizará o proxy para chamar a API.

## Tecnologias (sumário) 
- **Frontend**: Angular 16, TypeScript, RxJS, Angular Material (opcional).
- **Backend**: ASP.NET Core 7, C# 12, Entity Framework Core, JWT.
- **Banco relacional**: PostgreSQL 15.
- **Banco NoSQL**: MongoDB 6 (chat/coleções).
- **Containerização**: Docker + docker‑compose (opcional).
- **CI/CD**: GitHub Actions / Azure Pipelines (exemplo na pasta `.github`).

## Documentação adicional
- **Padrões de código** – `.ai/standards.md`
- **Decisões de arquitetura (ADRs)** – `.ai/architecture.md`
- **Stack de tecnologias** – `.ai/tech-stack.md`
- **Regras de negócio** – `.ai/business-rules.md`
- **Plano de implementação da POC** – `plano_implementacao_poc.md`

## Contribuição
1. Fork o repositório.
2. Crie uma branch `feature/<descrição>`.
3. Siga as convenções definidas em `standards.md`.
4. Abra um Pull Request.

---
*Este README fornece apenas o essencial para iniciar a POC; detalhes adicionais podem ser encontrados nos documentos da pasta `.ai`.*
