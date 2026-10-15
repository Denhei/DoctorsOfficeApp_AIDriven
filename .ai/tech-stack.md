# Tech Stack

## Backend (C#)
- **Framework**: ASP.NET Core 8.0 (Web API, `net8.0`)
- **Language**: C# 12 (construtores primários, collection expressions, records)
- **ORM**: Entity Framework Core 8 (Npgsql.EntityFrameworkCore.PostgreSQL 8)
- **Database Drivers**: Npgsql (PostgreSQL), MongoDB.Driver 3.x (MongoDB)
- **Authentication**: ASP.NET Identity + Microsoft.AspNetCore.Authentication.JwtBearer (JWT HS256)
- **Configuração**: `IOptions<T>` tipadas (`JwtOptions`, `StorageOptions`), validadas na subida
- **Erros HTTP**: as exceções de domínio (`RegraDeNegocioException`, `ConflitoException`, `AcessoNegadoException`) viram `{ mensagem }` num único `IExceptionFilter` (`TratadorGlobalDeExcecoes`); falhas de infraestrutura continuam 500
- **Dependency Injection**: container nativo do ASP.NET Core
- **Mapping**: projeções `Expression<Func<Entidade, Dto>>` em `DTOs/Mapeamentos.cs`, usadas com `Select` (sem AutoMapper)
- **Testing**: xUnit 2.9, Microsoft.AspNetCore.Mvc.Testing, Testcontainers (PostgreSQL 16 e MongoDB 7 reais)

## Frontend (TypeScript)
- **Framework**: Angular 19 (componentes standalone, `inject()`, signals no `AuthService`)
- **Language**: TypeScript 5.6
- **UI**: CSS próprio em `styles.css` (sem Angular Material)
- **HTTP Client**: Angular HttpClient; endereço da API em `services/api-url.ts`
- **Testing**: Karma + Jasmine (padrão do Angular CLI, sem specs escritos)

## Databases
- **Relational**: PostgreSQL 16 (`docker-compose.yml`)
- **NoSQL**: MongoDB 7 (coleção `conversas`)

## DevOps / Containerization
- **Container Runtime**: Docker (necessário também para rodar os testes de integração)
- **Orchestration**: docker-compose (bancos locais)
