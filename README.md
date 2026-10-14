# Consultório – Prova de Conceito (POC)

## Visão geral
Esta POC demonstra um fluxo completo de **triagem, validação, agendamento, consulta e pós‑consulta** para um sistema de consultório médico. O projeto está dividido em três partes principais:
- **Frontend** – SPA em Angular.
- **Backend** – API REST em ASP.NET Core.
- **Bancos de dados** – PostgreSQL (dados relacionais) e MongoDB (chat).

## Estrutura de pastas
```
DoctorsOfficeApp_AIDriven/
├─ backend/
│  ├─ Consultorio.Api/        # API ASP.NET Core 8
│  └─ Consultorio.Api.Tests/  # Testes de caracterização (xUnit + Testcontainers)
├─ frontend/                  # Aplicação Angular 19
├─ docs/refactoring/          # Diagnóstico, revisão e Relatório de Modernização
├─ .ai/                       # Arquitetura (ADRs), padrões, regras de negócio, stack
└─ README.md
```

## Como executar localmente
### Pré‑requisitos
- .NET 6 SDK ou superior (`dotnet --version`).
- Node 20+ e npm (`node -v`).
- Angular CLI (`npm i -g @angular/cli`).
- Docker (para `docker compose up -d` dos bancos e para os testes de integração).

### Bancos
```bash
docker compose up -d
```
A senha do PostgreSQL em `appsettings.json` precisa ser a mesma do `docker-compose.yml`.

### Backend
```bash
cd backend/Consultorio.Api
dotnet run --urls "http://localhost:5000"
```
Na subida a API aplica as migrations e o seed (contas em `Data/DbInitializer.cs`, senha `Senha@123`). Teste rápido:
```bash
curl http://localhost:5000/api/health
```
Swagger em `http://localhost:5000/swagger`.

### Testes
```bash
cd backend
dotnet test Consultorio.Api.Tests
```
Sobem PostgreSQL e MongoDB em containers, rodam a API real e verificam o contrato HTTP de todos os endpoints (ver `docs/refactoring`).

### Frontend
```bash
cd frontend
npm ci
npx ng serve --open
```
O Angular abre `http://localhost:4200` e chama a API em `http://localhost:5000/api` (`src/app/services/api-url.ts`).

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
- **Refatoração (diagnóstico, revisão e relatório)** – `docs/refactoring/`

## Contribuição
1. Fork o repositório.
2. Crie uma branch `feature/<descrição>`.
3. Siga as convenções definidas em `standards.md`.
4. Abra um Pull Request.

---
*Este README fornece apenas o essencial para iniciar a POC; detalhes adicionais podem ser encontrados nos documentos da pasta `.ai`.*
