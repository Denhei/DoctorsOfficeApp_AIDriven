# Architecture Decisions (ADRs)

## ADR 001 – Use ASP.NET Core Web API for the backend
- **Status**: Accepted
- **Context**: Need a performant, cross‑platform REST service.
- **Decision**: Build the backend with ASP.NET Core 8.0 leveraging built‑in DI, middleware pipeline, and JWT authentication.
- **Consequences**: Easy integration with EF Core, good tooling, strong typed API.

## ADR 002 – Separate persistent stores
- **Status**: Accepted
- **Context**: Chat triage requires flexible schema, while core domain data needs relational consistency.
- **Decision**: Use PostgreSQL for relational data (patients, consultations, slots) and MongoDB for chat collections. The status of a triage lives only in PostgreSQL (`ColetaInicial.Status`); the Mongo document holds messages, current step and preliminary summary.
- **Consequences**: Two connection strings; writes to both stores are sequential (PostgreSQL first), no distributed transaction.

## ADR 003 – Frontend framework
- **Status**: Accepted
- **Context**: Must deliver a rich SPA with component model.
- **Decision**: Use Angular 19 (TypeScript) with standalone components and RxJS. No Angular Material.
- **Consequences**: Standard CLI, strong typing, built‑in routing.

## ADR 004 – Authentication & Authorization
- **Status**: Accepted
- **Context**: Role‑based access across multiple user types.
- **Decision**: Issue JWTs signed with HS256 carrying `role`, `instituicaoId`, `pacienteId` and `medicoId` claims. Backend validates via `Microsoft.AspNetCore.Authentication.JwtBearer`; controllers read the caller once into `UsuarioAtual` and pass it to services.
- **Consequences**: Simple stateless auth, `[Authorize(Roles="...")]` for coarse access, services never query the database just to discover who the caller is.

## ADR 005 – Single HTTP error policy
- **Status**: Accepted (refactoring, 2026-10)
- **Context**: Controllers repeated 27 `try/catch` blocks mapping exceptions to status codes, and some actions leaked 500 for expected business errors.
- **Decision**: Services throw the domain exceptions `RegraDeNegocioException` (400), `ConflitoException` (409) or `AcessoNegadoException` (403); one MVC `IExceptionFilter` writes `{ mensagem }`. Controllers contain no error mapping. BCL exceptions (configuration, disk, database) are never mapped and surface as 500.
- **Consequences**: Uniform contract for the frontend; adding a status code means adding one case in one place.

## ADR 006 – Characterization tests as the refactoring safety net
- **Status**: Accepted (refactoring, 2026-10)
- **Context**: The project had no tests; structural changes needed proof that behavior stayed the same.
- **Decision**: `backend/Consultorio.Api.Tests` runs the real API (WebApplicationFactory) against real PostgreSQL and MongoDB containers (Testcontainers), seeded by `DbInitializer`, and pins the HTTP contract of every endpoint. Pure domain logic (`RoteiroTriagem`) has fast unit tests.
- **Consequences**: `dotnet test` needs Docker; the suite takes seconds and runs sequentially against one seeded database.
