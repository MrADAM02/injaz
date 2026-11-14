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

## Workspaces, members & invitations

### Roles and permissions

Every workspace-scoped request declares a `Permission`, and the pipeline checks it against the caller's role
before the handler runs. Non-members get `404 workspace.notFound`, so they can't tell whether the workspace
exists. Members whose role is too low get `403 auth.forbidden`.

| Permission | Viewer | Member | Admin | Owner | Used for |
|---|:-:|:-:|:-:|:-:|---|
| `View` | ✓ | ✓ | ✓ | ✓ | read workspace, members, boards, cards, activity; leave the workspace |
| `EditContent` | | ✓ | ✓ | ✓ | lists, cards, labels, checklists, comments; create/rename/archive boards |
| `DeleteContent` | | | ✓ | ✓ | hard-delete boards/lists/cards; delete others' comments *(comments: phase 5)* |
| `ManageMembers` | | | ✓ | ✓ | invite, change roles (never to/from owner), remove members |
| `ManageWorkspace` | | | ✓ | ✓ | rename |
| `OwnWorkspace` | | | | ✓ | delete workspace, transfer ownership |

Roles are stored as a number (Viewer 0 … Owner 3), so SQL can sort and compare them. The `Workspace` aggregate
enforces exactly one owner. Every membership change also updates the workspace row, so its `xmin` concurrency
token stops two admins from both applying edits made on a stale view.

### Endpoints

| Endpoint | Permission |
|---|---|
| `GET /api/workspaces` · `POST /api/workspaces` | signed in (creator becomes owner) |
| `GET /api/workspaces/{id}` | View |
| `PATCH /api/workspaces/{id}` `{ name }` | ManageWorkspace |
| `DELETE /api/workspaces/{id}` | OwnWorkspace |
| `POST /api/workspaces/{id}/transfer-ownership` `{ userId }` | OwnWorkspace (old owner becomes admin) |
| `GET /api/workspaces/{id}/members` | View |
| `PATCH /api/workspaces/{id}/members/{userId}` `{ role }` | ManageMembers |
| `DELETE /api/workspaces/{id}/members/{userId}` | View for yourself (leave), ManageMembers for others |
| `GET` · `POST /api/workspaces/{id}/invitations` `{ email, role }` | ManageMembers |
| `DELETE /api/workspaces/{id}/invitations/{invitationId}` | ManageMembers |
| `GET /api/invitations/{token}` | public (preview for the invite page) |
| `POST /api/invitations/{token}/accept` | signed in, with the invited email |

### How invitations work

- An invitation is for one email address and one role (never owner). It expires after 7 days.
- The response to `POST …/invitations` contains the secret `token` and `acceptUrl`
  (`{Invitations:FrontendBaseUrl}/invite/{token}`). This is the only time they're available, because only a
  hash is stored.
- Inviting the same email again revokes the previous link.
- Only the account with the invited email can accept (`422 invitation.emailMismatch`). Spent links return
  `422 invitation.accepted`, `invitation.revoked` or `invitation.expired`.
- The invitation email is sent only after the command commits, through `IAfterCommit`. The MVP `IEmailSender`
  only logs the recipient and subject; the body, which contains the link, is logged at Debug level. To send
  real email, plug in a provider by implementing `IEmailSender`.

| Key | Default | Notes |
|---|---|---|
| `Invitations:Lifetime` | `7.00:00:00` | |
| `Invitations:FrontendBaseUrl` | `""` | Development: `http://localhost:3000`. Set it in production. |

## Boards, lists & cards

### Endpoints

| Endpoint | Permission |
|---|---|
| `GET /api/workspaces/{id}/boards?includeArchived=` · `POST` `{ title, description?, color? }` | View · EditContent |
| `GET /api/boards/{id}`: the whole board (open lists → open cards, in order, plus labels and members) | View |
| `PATCH /api/boards/{id}` `{ title?, description?, color?, isArchived? }` | EditContent |
| `DELETE /api/boards/{id}` | DeleteContent |
| `POST /api/boards/{id}/labels` `{ name?, color }` · `PATCH`/`DELETE /api/labels/{id}` | EditContent |
| `POST /api/boards/{id}/lists` `{ title }` (appended) · `PATCH /api/lists/{id}` `{ title?, isArchived? }` | EditContent |
| `POST /api/lists/{id}/move` `{ afterId?, beforeId? }` | EditContent |
| `DELETE /api/lists/{id}` (its cards too) | DeleteContent |
| `POST /api/lists/{id}/cards` `{ title, position?: "bottom" \| "top" }` | EditContent |
| `GET /api/cards/{id}` · `PATCH` `{ title?, description?, dueAt?, priority?, isCompleted?, isArchived? }` | View · EditContent |
| `POST /api/cards/{id}/move` `{ listId?, afterId?, beforeId? }` | EditContent |
| `DELETE /api/cards/{id}` (members archive instead) | DeleteContent |
| `PUT`/`DELETE /api/cards/{id}/assignees/{userId}` · `PUT`/`DELETE /api/cards/{id}/labels/{labelId}` (idempotent) | EditContent |

**Colors** come from a named palette: `green yellow orange red purple blue sky lime pink gray`. Names are
stored rather than hex values, so the frontend picks accessible light and dark shades.

**PATCH follows JSON merge-patch rules.** An omitted field is left unchanged, and an explicit `null` clears it
(`{ "dueAt": null }`). This is implemented with `Optional<T>` and a JSON converter. Due dates are stored in UTC.

**Cross-resource checks.** Every request is authorized on its own resource (board, list, card or label). Any
*other* id in the request must belong to the same board or workspace, otherwise it gets a 404:
- a move's target list,
- a label being attached,
- a user being assigned.

Removing a member from a workspace also unassigns them from its cards.

### Ordering (drag & drop)

Lists and cards have a `rank`: a base-62 fractional key, stored as `COLLATE "C"` so that SQL ordering matches
ordinal string order. Order is `(rank, id)`.

**Moving an item:**
- The client sends the ids of the items it sees directly above and below the drop point. Either one may be
  omitted; with neither, the item goes to the end.
- The server computes a key between those two neighbours and updates **only the moved row**.
- If a neighbour has meanwhile moved away or been deleted, the server returns
  `409 ordering.staleNeighbor`, and the client reloads the board.

**Keeping keys short.**
- Appending bumps the last digit, so a key gains about one character per 36 appends. Inserting between two
  items halves the gap, so it gains about one character per 6 inserts in the same spot.
- If keys pass 64 characters, or two concurrent drops produce equal ranks, the server re-spreads **that one
  list** in the same transaction. This fallback is rare and bounded; it is never a global reindex.

**Concurrent edits.** If two people move or edit the same card at once, the `xmin` concurrency check stops the
second change and returns `409 conflict.concurrency`.
