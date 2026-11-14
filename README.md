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

## Authentication

| Endpoint | Auth | Notes |
|---|---|---|
| `POST /api/auth/register` | — | 201 + session. Rate-limited. |
| `POST /api/auth/login` | — | Same error for unknown email and wrong password. Rate-limited. |
| `POST /api/auth/refresh` | refresh cookie + `X-Requested-With` | Rotates the refresh token. |
| `POST /api/auth/logout` | refresh cookie + `X-Requested-With` | Ends this session only. Idempotent. |
| `GET /api/me` · `PATCH /api/me` | Bearer | PATCH: omitted/null = unchanged; `avatarUrl: ""` removes it. |
| `PUT /api/me/password` | Bearer | Signs out every other session; returns a fresh one. Rate-limited. |

**Tokens.**
- The access token is a 15-minute JWT (HS256) that carries only `sub`. Workspace roles are checked against the
  database on every request, so a role change takes effect immediately.
- The access token is returned in the JSON body. The frontend keeps it in memory only.
- The refresh token is 256 random bits. The database stores only its SHA-256 hash.

**The refresh cookie.**
- Settings: `injaz_rt`, `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`, host-only (no `Domain`).
- The app (`app.<domain>`) and the API (`api.<domain>`) are same-site, so the browser still sends the cookie
  on credentialed `fetch`. JavaScript can never read it.

**Rotation and reuse detection.**
- Every refresh revokes the presented token and issues its successor in the same *family*.
- Replaying a token rotated in the last 30 s returns `auth.refreshSuperseded`. The session survives, because
  this is usually two tabs refreshing at once; the client should retry once with the cookie it now has.
- Replaying an older rotated token means it was probably stolen, so the whole family is revoked.
- If two refreshes race on the same token, PostgreSQL's `xmin` concurrency check lets only one win.

**CSRF.** Cookie-authenticated endpoints have two protections: `SameSite=Strict`, and a required
`X-Requested-With` header. A cross-origin request can't send that header without passing a CORS preflight,
and only `Cors:AllowedOrigins` passes.

**Passwords.** PBKDF2-HMAC-SHA512 with 210k iterations, using only the BCL. Hashes upgrade automatically on
login when the iteration count is raised.

### Configuration

| Key | Default | Notes |
|---|---|---|
| `Jwt:SigningKey` | *(empty — startup fails)* | **Required.** At least 32 bytes. Set via env `Jwt__SigningKey` in production. |
| `Jwt:AccessTokenLifetime` | `00:15:00` | |
| `Auth:RefreshTokenLifetime` | `30.00:00:00` | Sliding: each refresh extends it. |
| `Auth:RefreshReuseGracePeriod` | `00:00:30` | |
| `Auth:RefreshCookie:Secure` | `true` | `false` in Development (plain-http localhost). |
| `Cors:AllowedOrigins` | `[]` | Development: `http://localhost:3000`. |
| `RateLimiting:CredentialsPermitLimit` / `CredentialsWindow` | `10` / `00:01:00` | Per client IP. |
