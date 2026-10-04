# Architecture Decisions (ADRs)

## ADR 001 – Use ASP.NET Core Web API for the backend
- **Status**: Accepted
- **Context**: Need a performant, cross‑platform REST service.
- **Decision**: Build the backend with ASP.NET Core 7.0 leveraging built‑in DI, middleware pipeline, and JWT authentication.
- **Consequences**: Easy integration with EF Core, good tooling, strong typed API.

## ADR 002 – Separate persistent stores
- **Status**: Accepted
- **Context**: Chat triage requires flexible schema, while core domain data needs relational consistency.
- **Decision**: Use PostgreSQL for relational data (patients, consultations, slots) and MongoDB for chat collections.
- **Consequences**: Two connection strings; need to manage transactions across stores manually where needed.

## ADR 003 – Frontend framework
- **Status**: Accepted
- **Context**: Must deliver a rich SPA with component model.
- **Decision**: Use Angular 16 (TypeScript) with RxJS and Angular Material for UI components.
- **Consequences**: Standard CLI, strong typing, built‑in routing.

## ADR 004 – Authentication & Authorization
- **Status**: Accepted
- **Context**: Role‑based access across multiple user types.
- **Decision**: Issue JWTs signed with HS256, embed `role` claim. Backend validates via `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **Consequences**: Simple stateless auth, easy to enforce `[Authorize(Roles="..." )]`.

