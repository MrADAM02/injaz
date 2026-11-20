# Injaz (إنجاز)

Task & project management — Kanban boards plus a personal "My Tasks" view. Arabic (RTL, default) and English.

| Part | Stack |
|---|---|
| Backend | ASP.NET Core 10 (LTS), Clean Architecture, hand-rolled CQRS dispatcher, EF Core + PostgreSQL 18 |
| Frontend | Nuxt 4 SPA (static), TypeScript, Pinia, Tailwind CSS 4, @nuxtjs/i18n |
| Real-time | SignalR (WebSockets, falls back to SSE / long polling) |

## Prerequisites

- .NET SDK 10.0.1xx or newer (`global.json` rolls forward to the latest feature band)
- Docker Desktop (local stack + integration tests use Testcontainers)
- Node.js 22.12+ (24 LTS recommended) for the frontend

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

### Frontend

```bash
docker compose up -d                   # API on :8080
cd frontend
npm install
npm run dev                            # http://localhost:3040
npm run dev:lan                        # same, reachable from a phone on your network
```

The dev server proxies `/api` and `/hubs` to the API (`INJAZ_DEV_API`, default `http://localhost:8080`). The
app is therefore same-origin in development, and a phone on the LAN works without any CORS or cookie setup.

## Test

```bash
cd backend
dotnet test --solution Injaz.slnx      # Docker must be running (integration tests start a Postgres container)

cd frontend
npm run check                          # RTL lint + type check + unit tests
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
| `Cors:AllowedOrigins` | `[]` | Development: `http://localhost:3040`. |
| `RateLimiting:CredentialsPermitLimit` / `CredentialsWindow` | `10` / `00:01:00` | Per client IP. |
| `ReverseProxy:TrustedNetworks` | `[]` | CIDRs allowed to set the client IP/scheme via `X-Forwarded-For`/`-Proto`. Production: the private ranges (see [Deployment](#deployment)). |
| `Invitations:FrontendBaseUrl` | *(empty)* | Base of invite links. Production fails at startup without it and without `Cors:AllowedOrigins:0`. |
| `Database:MigrateOnStartup` | `false` | `true` in Development and Production (single instance). |

## Workspaces, members & invitations

### Roles and permissions

Every workspace-scoped request declares a `Permission`, and the pipeline checks it against the caller's role
before the handler runs. Non-members get `404 workspace.notFound`, so they can't tell whether the workspace
exists. Members whose role is too low get `403 auth.forbidden`.

| Permission | Viewer | Member | Admin | Owner | Used for |
|---|:-:|:-:|:-:|:-:|---|
| `View` | ✓ | ✓ | ✓ | ✓ | read workspace, members, boards, cards, activity; leave the workspace |
| `EditContent` | | ✓ | ✓ | ✓ | lists, cards, labels, checklists, comments; create/rename/archive boards |
| `DeleteContent` | | | ✓ | ✓ | hard-delete boards/lists/cards; delete others' comments |
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
| `Invitations:FrontendBaseUrl` | `""` | Development: `http://localhost:3040`. Set it in production. |

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
| `GET /api/boards/{id}/lists/archived`: archived lists, in board order, as `{ id, title, cardCount }` (restore one with `PATCH /api/lists/{id}` `{ isArchived: false }`) | View |
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

## Checklists, comments, activity, My Tasks & search

### Endpoints

| Endpoint | Permission |
|---|---|
| `POST /api/cards/{id}/checklist` `{ text }` (appended) | EditContent |
| `PATCH /api/checklist-items/{id}` `{ text?, isDone? }` · `POST …/move` `{ afterId?, beforeId? }` · `DELETE` | EditContent |
| `GET /api/cards/{id}/comments?cursor=&limit=` (newest first) | View |
| `POST /api/cards/{id}/comments` `{ body }` | EditContent |
| `PATCH /api/comments/{id}` `{ body }` | author only |
| `DELETE /api/comments/{id}` | author, or DeleteContent for anyone's |
| `GET /api/boards/{id}/activity?cursor=&limit=` · `GET /api/cards/{id}/activity?…` | View |
| `GET /api/boards/{id}/cards?q=&labelIds=&assigneeIds=&due=&tz=&archived=&cursor=&limit=` | View |
| `GET /api/me/tasks?due=&priority=&workspaceId=&includeCompleted=&tz=&cursor=&limit=` | signed in |

**Card detail and tiles.**
- `GET /api/cards/{id}` now includes the checklist and a comment count.
- Card tiles (board, My Tasks, search) show badges: `checklistDone`, `checklistTotal` and `commentCount`.
- All tiles come from one shared SQL projection, so they look the same everywhere.

### Pagination

Lists that can grow (comments, activity, search, My Tasks) use **keyset** pagination: `{ items, nextCursor }`.
Pass `nextCursor` back as `?cursor=` to get the next page. `limit` is 1–100 (default 30). Unlike offsets,
pages stay stable while new rows are being added.

### Activity log

- **Written inside the same transaction as the change**, through `IActivityLog`. A rolled-back command never
  leaves an entry, and a committed one always has its entry.
- **Each entry has** a `type` (for example `card.moved`), an `actor`, and a small `data` JSON object holding
  names and titles *as they were*. The feed therefore still reads correctly after things are renamed or deleted.
- **Deleting a card** keeps its history on the board: the entries lose their `cardId` reference but stay in
  the feed.
- **Only real changes are logged.**
  - Reordering cards inside a list is skipped.
  - Repeating an idempotent PUT/DELETE logs nothing.
  - A PATCH logs one entry per field that actually changed.
- **Ordering:** entries recorded by the same request get strictly increasing timestamps, so the feed always
  shows them in the order they were recorded.

**Types:**
- `board.created|renamed|archived|restored`
- `list.created|renamed|archived|restored|deleted`
- `card.created|renamed|descriptionChanged|dueChanged|priorityChanged|completed|reopened|archived|restored|moved|deleted|assigned|unassigned|labelAdded|labelRemoved`
- `checklist.itemAdded|itemCompleted|itemReopened|itemDeleted`
- `comment.added`

### My Tasks & due buckets

My Tasks shows the open cards assigned to you, across all your workspaces. Cards on archived lists or boards
are excluded. They are sorted soonest due first, with undated cards last.

`due` is one of:

| Bucket | Meaning |
|---|---|
| `overdue` | due before now and not completed |
| `today` | due during your current calendar day |
| `week` | due from the start of today through the next 7 days |
| `none` | no due date |

"Today" depends on where you are, so the client sends its IANA time zone (`tz=Asia/Riyadh`). The default is
UTC. The runtime image includes tzdata.

### Board search

Search matches text in the title or description (substring, case-insensitive). It also filters by *any of*
`labelIds` / `assigneeIds`, by a due bucket, and by open vs archived (`archived=true` returns only archived
cards). Results are ordered most recently updated first.

The board screen filters its already-loaded cards in the browser. This endpoint covers what that payload
doesn't include: text inside descriptions, and archived cards.

## Real-time (SignalR)

**Hub:** `/hubs/boards`. Authentication uses the same JWT access token as REST. Browsers can't set headers on
WebSocket connections, so the token is also accepted as `?access_token=`, but **only on `/hubs/*`**. Don't let
the reverse proxy log query strings for that path. A connection closes when its token expires, and the client
reconnects with a fresh token (`accessTokenFactory`).

### Client contract

```ts
const hub = new HubConnectionBuilder()
  .withUrl(`${api}/hubs/boards`, { accessTokenFactory: () => auth.accessToken })
  .withAutomaticReconnect()
  .build()

hub.on('boardEvent', (e: BoardEvent) => store.apply(e))  // see below
hub.on('accessRevoked', ({ workspaceId }) => leaveWorkspace(workspaceId))
await hub.start()
await hub.invoke('JoinBoard', boardId)                     // same rule as GET /api/boards/{id}
// on reconnect: invoke JoinBoard again, then refetch GET /api/boards/{id} to catch up
```

REST calls should send `X-Connection-Id: <hub.connectionId>`. The connection that made a change is then skipped
for that change's event, because it has already applied the change optimistically.

`BoardEvent = { type, boardId, actorId, payload, occurredAt }`:

| `type` | `payload` | Client action |
|---|---|---|
| `board.updated` | board summary | update the header; leave the screen if `isArchived` |
| `board.deleted` | `{ id }` | leave the board |
| `list.upserted` | list (without cards) | insert or update the column, ordered by `rank`. **Unknown id** (e.g. a restored list): refetch the board |
| `list.removed` | `{ id }` | drop the column and its cards (archived or deleted) |
| `card.upserted` | card tile (same shape as `GET /api/boards/{id}`) | insert, update or move the tile by `listId` + `rank`; if its dialog is open, refetch the card |
| `card.removed` | `{ id }` | drop the tile (archived, deleted, or its list was archived) |
| `label.upserted` / `label.removed` | label / `{ id }` | update the palette; on removal also strip it from tiles |

### How it works

- **Handlers report what changed.** For example, `events.CardChanged(boardId, cardId)`.
- **One flush per request, after the commit.** It runs through `IAfterCommit` and does four things:
  - reloads the **committed** state,
  - removes duplicates (a PATCH of five fields sends one event),
  - turns archived items into removals,
  - publishes.
  A command that rolls back broadcasts nothing.
- **Rebalances are included.** When a rebalance re-spreads a list, every re-ranked sibling is republished, so
  clients that sort by `rank` stay in order.
- **Removing a member** pulls that user's live connections out of the workspace's board groups, and sends
  them `accessRevoked`.
- **Transport separation.** The Application layer owns the semantics (`IBoardEvents`). The API layer only owns
  delivery (`IRealtimePublisher` implemented on SignalR).
- **Single instance.** The connection registry is in memory, which matches the single-VM deployment. Running
  several API instances would need a SignalR backplane (e.g. Redis) and a shared registry.

## Frontend (Nuxt 4 SPA)

`ssr: false`, built with `nuxt generate` into static files (`.output/public`) and hosted on Netlify (see
[Deployment](#deployment)). All data comes from the API at runtime. `NUXT_PUBLIC_API_BASE` sets the API at build
time (empty means same origin, e.g. the dev proxy).

```
frontend/
  i18n/locales/{ar,en}.json   messages (Arabic default); i18n.config.ts adds Arabic's six plural forms
  app/
    app.vue                   <html lang dir> follow the active locale
    layouts/                  default (sidebar · phone top bar + drawer + tab bar), auth (centered card)
    middleware/auth.global.ts restores the session once, then gates routes (private unless `public: true`)
    pages/                    login, register, invite/[token], profile, my-tasks, w/[id] (boards, settings),
                              b/[id] (board) + b/[id]/c/[cardId] (card dialog as a child route)
    components/ui/            Button, Input, Select, Modal (native <dialog>, bottom sheet on phones), Toaster, …
    components/layout/        Sidebar, Topbar, BottomNav, LocaleSwitcher
    components/workspace/     board tiles/dialogs, members, invitations, role badge
    components/board/         Column, CardTile, composers, filter/search/labels/edit dialogs, ActivityFeed
    components/card/          Description (Markdown), Checklist, Comments, pickers, due date, Move to…
    stores/                   auth (in-memory token), workspaces, board, realtime, toast, confirm, ui
    composables/useSortable   SortableJS drag & drop (see below)
    utils/                    api-client, ordering, cards, activity, markdown, roles, validate, palette, bidi …
                              (framework-free, unit-tested)
  scripts/check-logical-css.mjs   fails on physical left/right styles
```

### Board view

- **One store for the board on screen** (`stores/board.ts`): lists in rank order, each holding its cards in
  rank order. Ordering uses `(rank, id)` with code-unit comparison, which matches the server's `COLLATE "C"`.
- **Optimistic moves.** A drop updates the store right away and sends `{ listId, afterId, beforeId }`: the
  cards directly above and below the drop point. The response carries the new rank. If the move fails (for
  example `409 ordering.staleNeighbor`), the board reloads and a toast explains why.
- **Drag & drop** uses SortableJS in fallback mode, so mouse, touch and pen behave the same and edge auto-scroll
  works. On touch, a long press (200 ms) starts a drag and a plain swipe scrolls. Sortable moves DOM nodes
  itself; on drop the node is put back and the store update re-renders, so Vue and the DOM never disagree.
  Columns are dragged by their grip.
- **Keyboard and small screens.** The card dialog has a **Move to…** action (list + top/bottom). Column menus
  have *Move earlier / later*. Both use the same move endpoints.
- **Filters** (text, labels, members, due bucket) run in the browser over the loaded cards and live in the URL
  (`?q=&labels=&members=&due=`). While a filter is on, cards can't be dragged: the neighbours of a drop would
  be guesses. **Search** uses `GET /boards/{id}/cards` for what the board payload doesn't hold: text inside
  descriptions, and archived cards (which can be restored from there).
- **Phones.** Columns are 85 vw wide and snap one per swipe, with dots underneath to jump between them. Snapping
  is paused while dragging. The card dialog is full screen, and the back gesture closes it.
- **The card dialog is a child route** (`/b/:boardId/c/:cardId`), so it can be linked and reloaded. Closing goes
  back in history when the dialog was opened from the board, and replaces the URL otherwise. It reads
  `GET /api/cards/{id}`; each change PATCHes and then updates the tile from the response.
- **Markdown** (descriptions, comments) goes through markdown-it with raw HTML off, then DOMPurify. Links open
  in a new tab with `rel="noopener noreferrer nofollow"`. Each paragraph takes its direction from its own
  text (`unicode-bidi: plaintext`).
- **Activity lines** are built from the entry's `type` and `data` (`utils/activity.ts`). Names are
  bidi-isolated, so English titles read correctly inside Arabic sentences.

### Live updates

- **One SignalR connection per tab** (`stores/realtime.ts`) follows whichever board is on screen.
  - It retries forever: 0 s, 1 s, 3 s, 10 s, then every 30 s.
  - After a reconnect it re-joins the board and reloads it, to catch up on missed events.
  - While it is reconnecting, the board shows a banner.
- **No echo of our own changes.** REST calls send `X-Connection-Id`, so the server leaves our own changes out.
- **Events wait during a drag or a pending move.** Applying them then would re-render the lists under the
  pointer, or place cards around a tile that still carries its old rank.
- **Event handling.**
  - `card.upserted` places the tile by `listId` + `rank`. If that card's dialog is open, it re-reads the card.
  - An unknown list triggers a full board reload.
  - `board.updated` (archived), `board.deleted` and `accessRevoked` leave the board with a message.

### Session handling

- The access token lives **only in memory**. A page load restores the session with one
  `POST /api/auth/refresh`, which sends the httpOnly cookie.
- The API client attaches the token and refreshes about 30 s before it expires. On a `401` it refreshes once and
  retries; if that fails, the session is over and the client goes to `/login?redirect=…`.
- Refreshes are **single-flight**: parallel requests share one refresh. `auth.refreshSuperseded` (another tab
  rotated the cookie a moment ago) is retried once.
- Signing out in one tab signs out every tab, through a `BroadcastChannel`.
- `?redirect=` only accepts in-app paths, so the login page can't be used as an open redirect.

### Errors and validation

Forms check the same rules as the API, with the same codes (`required`, `maxLength`…), before sending.
Server errors are translated by their ProblemDetails `code`: `errors.<code>` for the whole request, and
`validation.<code>` with its params for each field. A unit test checks that every error code thrown in the C#
sources has a translation, and that both locale files have the same keys and placeholders.

### RTL and mobile

- **Logical properties only.** Use `ms/me`, `ps/pe`, `inset-s/e`, `border-s/e`, `rounded-s/e` and `text-start`.
  `npm run lint:rtl` enforces it. Directional icons are mirrored with `rtl:`.
- **LTR values inside Arabic text.** Emails, URLs and passwords are rendered LTR. When they appear inside a
  translated sentence, they are wrapped in Unicode isolates (`isolateLtr`).
- **Phone layout.** Phones get a top bar, a drawer that slides in from the start edge, and a bottom tab bar.
  Dialogs become bottom sheets.
- **Touch and small screens.**
  - Tap targets are at least 44 px.
  - Inputs are at least 16 px, so iOS doesn't zoom in.
  - Layout respects `env(safe-area-inset-*)` and uses `dvh` units.
  - Selects are native, so phones show their own picker.
- **Locale preference.** It is stored on the profile and follows the user to every device. When signed out, the
  last choice on this device is used, with Arabic as the default.

## Deployment

| Part | Where | How it updates |
|---|---|---|
| Frontend | Netlify, at `https://injaz.jab-eri.org` | Netlify builds `frontend/` on every push to `main` ([netlify.toml](netlify.toml)). |
| API + PostgreSQL | The Hetzner server, `/opt/injaz`, at `https://injaz-api.jab-eri.org` | [Backend workflow](.github/workflows/backend.yml): test → image `ghcr.io/mradam02/injaz-api:sha-…` → SSH deploy. |

The frontend **must** stay on a `jab-eri.org` subdomain: the refresh cookie is `SameSite=Strict` and host-only on
the API, so browsers only send it from the same site. On a `*.netlify.app` address people would be signed out
on every reload.

### The server

The server also runs other live projects, so Injaz follows their conventions and stays in its own lane:

- [deploy/docker-compose.yml](deploy/docker-compose.yml) → `/opt/injaz/docker-compose.yml`. The API listens on
  `127.0.0.1:5003` only; PostgreSQL has no published port. Memory/CPU limits (API 384 MB, PostgreSQL 256 MB, one
  CPU each) and rotated logs keep it from starving the other projects or filling the disk.
- [deploy/.env.example](deploy/.env.example) → `/opt/injaz/.env` (`chmod 600`), with secrets from `openssl rand -hex`.
- [deploy/nginx/injaz-api.jab-eri.org.conf](deploy/nginx/injaz-api.jab-eri.org.conf) → the host nginx, with TLS from
  certbot like the other sites. `/hubs/` gets WebSocket headers and no access log (tokens travel in its query string).
- Migrations run when the API starts (`appsettings.Production.json`).
- The API trusts `X-Forwarded-For` only from private networks (nginx reaches the container through Docker's
  bridge). That is safe because the port is bound to `127.0.0.1`: nothing outside the server can reach it directly.

### First deploy (manual, once)

1. **DNS (Cloudflare, DNS only):** `A injaz-api → 91.98.168.57`; later `CNAME injaz → <site>.netlify.app`.
2. **Image:** merge to `main`; the Backend workflow publishes `ghcr.io/mradam02/injaz-api:latest` and `:sha-…`.
3. **Server:**
   ```bash
   sudo mkdir -p /opt/injaz && sudo chown "$USER": /opt/injaz && cd /opt/injaz
   # copy deploy/docker-compose.yml here, and deploy/.env.example as .env; fill it in
   chmod 600 .env
   docker compose pull && docker compose up -d
   curl -s http://127.0.0.1:5003/health          # → Healthy
   ```
4. **nginx + TLS:**
   ```bash
   sudo cp injaz-api.jab-eri.org.conf /etc/nginx/sites-available/
   sudo ln -s /etc/nginx/sites-available/injaz-api.jab-eri.org.conf /etc/nginx/sites-enabled/
   sudo nginx -t && sudo systemctl reload nginx  # never reload if -t fails: other sites share this nginx
   sudo certbot --nginx -d injaz-api.jab-eri.org
   curl -s https://injaz-api.jab-eri.org/health  # → Healthy
   ```
5. **Netlify:** import the GitHub repo and leave the build fields empty (`netlify.toml` provides them). Add the
   custom domain `injaz.jab-eri.org` and let Netlify issue its certificate.
6. **Automatic deploys:** in the GitHub repo add the secrets `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY` (as for
   TrainingPlatform) and the variable `DEPLOY_ENABLED=true`. From then on every backend push to `main` deploys and
   then checks `/health`.

### Operations

- **Logs:** `cd /opt/injaz && docker compose logs -f api` (JSON lines).
- **Roll back:** `API_VERSION=sha-<older commit> docker compose up -d`.
- **Backup:** `docker compose exec -T postgres pg_dump -U injaz -Fc injaz > injaz-$(date +%F).dump`; restore with
  `pg_restore -U injaz -d injaz --clean` inside the container.

