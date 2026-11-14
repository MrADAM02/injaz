# Injaz (إنجاز)

Task & project management — Kanban boards plus a personal "My Tasks" view. Arabic (RTL, default) and English.

| Part | Stack |
|---|---|
| Backend | ASP.NET Core 10 (LTS), Clean Architecture, hand-rolled CQRS dispatcher, EF Core + PostgreSQL 18 |
| Frontend | Nuxt 4 SPA, TypeScript, Pinia, Tailwind *(phase 7)* |
| Real-time | SignalR *(phase 6)* |

## Prerequisites

- .NET SDK 10.0.1xx or newer (`global.json` rolls forward to the latest feature band)
- Docker Desktop (local stack + integration tests use Testcontainers)

## Run locally

```bash
# Everything in containers → http://localhost:8080
docker compose up --build

# Or: database in Docker, API from your IDE / CLI (hot reload, debugger)
docker compose up -d postgres
cd backend && dotnet run --project src/Injaz.Api        # http://localhost:8080
```

PostgreSQL is exposed on host port **5433** (not 5432) so it never clashes with a locally installed Postgres.
Credentials: `injaz` / `injaz_dev`, database `injaz` — local development only.

In Development, migrations are applied on startup (`Database:MigrateOnStartup`). Smoke requests live in
[backend/src/Injaz.Api/Injaz.Api.http](backend/src/Injaz.Api/Injaz.Api.http); the OpenAPI document is at `/openapi/v1.json`.

## Test

```bash
cd backend
dotnet test --solution Injaz.slnx      # Docker must be running (integration tests start a Postgres container)
```

## Migrations

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Name> --project src/Injaz.Infrastructure --startup-project src/Injaz.Api --output-dir Persistence/Migrations
```

## Backend layout

```
backend/src
  Injaz.Domain          entities, invariants, value objects — no dependencies
  Injaz.Application     use cases (Features/<Area>/<UseCase>.cs), dispatcher, validation, access policies
  Injaz.Infrastructure  EF Core (AppDbContext, configurations, migrations), security, SignalR, email
  Injaz.Api             thin controllers, ProblemDetails mapping, HTTP plumbing
```

### Request pipeline

Every request goes through `IDispatcher`: **authorize → validate → handle → commit** (commit for commands only).

- **Access policy is mandatory.** Each request implements `IPublicRequest` or `IAuthenticatedRequest`
  (workspace-role checks arrive in phase 3). A request without one fails closed, and an architecture test fails the build.
- **Validation** is hand-rolled (`IValidator<T>` + `ValidationContext.RuleFor(...)`), discovered by assembly scan.
- **One transaction per command.** Handlers never call `SaveChanges`; the dispatcher does, after the handler succeeds.

### Error contract

All errors are RFC 9457 ProblemDetails (`application/problem+json`) with a stable `code` the frontend translates:

```json
{
  "status": 400,
  "title": "Bad Request",
  "code": "validation.failed",
  "errors": { "title": [{ "code": "maxLength", "params": { "max": 100 } }] },
  "traceId": "00-…"
}
```

| Status | When | Example `code` |
|---|---|---|
| 400 | validation / unreadable body | `validation.failed` |
| 401 | not signed in | `auth.unauthorized` |
| 403 | missing role | `auth.forbidden` |
| 404 | not found | `card.notFound` |
| 409 | duplicate / concurrent edit | `conflict.duplicate`, `conflict.concurrency` |
| 422 | business rule violated | e.g. `workspace.lastOwner` |
| 500 | bug — details never leaked outside Development | `server.error` |
