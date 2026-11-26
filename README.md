# LibreSharp WolverineFx + React Template

A `dotnet new` template pairing a [WolverineFx](https://wolverinefx.net/) backend (HTTP
endpoints + message bus) with a React ([Vite](https://vite.dev/) + TypeScript + Tailwind CSS)
frontend, built as a single deployable ASP.NET Core host.

Part of [LibreSharp](https://libresharp.github.io), which collects free alternatives to .NET
packages that went commercial. This template does the jobs often given to MediatR (in-process
messaging) and MassTransit (message bus and transactional outbox) with Wolverine, on a stack of
free, open-source libraries. It is a community template, not an official WolverineFx/JasperFx
package.

## Prerequisites

- **.NET 11 SDK.** Check with `dotnet --list-sdks`. If you installed it with the
  `dotnet-install` script, it lives in `~/.dotnet`, which isn't on your `PATH` by default: add
  `export DOTNET_ROOT=$HOME/.dotnet` and `export PATH=$DOTNET_ROOT:$PATH` to your shell profile.
- **Node.js 22 or later**, with npm.
- **Docker with the Compose plugin.** Postgres runs in Docker for development, and the tests
  start their own throwaway Postgres container. Check with `docker compose version`. On
  Ubuntu/Debian/Mint, the `docker.io` package doesn't include Compose: also install
  `docker-compose-v2`, and add yourself to the `docker` group
  (`sudo usermod -aG docker $USER`, then log out and back in).

## Install

```bash
dotnet new install LibreSharp.Templates.WolverineFxReact
```

(Not on nuget.org yet. Until then, install from a clone: `dotnet new install ./`.)

## Usage

```bash
dotnet new wolverine-react -n MyApp
cd MyApp
docker compose up -d          # starts Postgres
cd src/Client && npm install
cd ../MyApp && dotnet run      # API on the port shown; Vite dev server proxies /api to it
```

In a separate terminal:

```bash
cd MyApp/src/Client
npm run dev                    # http://localhost:5173, proxies /api/* to the API
```

With `--Identity true`, a demo user (`demo@example.com` / `Demo123!`) is created at startup in
Development only, and the dev server pre-fills the login form with it. Change it under
`DevelopmentUser` in `appsettings.Development.json` and in `src/Client/.env.development`.

### Parameters

| Parameter | Values | Default | Effect |
|---|---|---|---|
| `--Persistence` | `marten` \| `efcore` | `marten` | Document DB/event sourcing over PostgreSQL (Marten) vs. relational EF Core over PostgreSQL |
| `--Identity` | `true` \| `false` | `false` | Adds JWT-based auth (register/login/refresh/logout, rotating refresh tokens) |
| `--IncludeTests` | `true` \| `false` | `true` | Includes the `tests/` project(s) |

Example:

```bash
dotnet new wolverine-react -n MyApp --Persistence efcore --Identity true
```

## What you get

- A vertical-slice `Features/Todos` example showing Wolverine HTTP endpoints
  (`[WolverineGet]`/`[WolverinePost]`) wired to either Marten or EF Core, and what Wolverine adds
  beyond plain endpoints:
  - **Messaging with a transactional outbox.** Completing a Todo cascades a `TodoCompleted`
    message to `TodoCompletedHandler`, which runs in the background. Local queues are durable,
    so the message is saved in the same database transaction as the Todo change: it survives a
    restart and is never sent if the change rolls back.
  - **Event sourcing** (`--Persistence marten`). Each Todo is an event stream (`TodoCreated`,
    `TodoCompleted`). The complete endpoint uses Wolverine's aggregate handler workflow
    (`[WriteAggregate]`): it receives the rebuilt Todo and returns the events to append and the
    messages to send, with an optimistic concurrency check. An inline snapshot projection keeps
    a queryable Todo document in step.
  - **Validation.** A FluentValidation validator rejects an empty or over-long title with a 400
    `ProblemDetails` response before the endpoint runs; the React form shows the message.
  - **Realtime updates with SignalR.** Creating or completing a Todo pushes a `todo-changed`
    message to the browser through Wolverine's SignalR transport (`AppHub` at `/api/hub`), so
    every open tab refreshes its list. Completion travels the whole chain: endpoint →
    `TodoCompleted` (outbox) → background handler → SignalR. With `--Identity true`, updates go
    only to the signed-in user's own tabs (a per-user SignalR group), and the hub requires login.
- Vite dev server proxies `/api/*` to the API in development (no CORS); in production, the
  client is built and served as static files from the same Kestrel host (`dotnet publish` builds
  the client automatically).
- **Production-ready code generation.** In Development, Wolverine generates its handler code at
  runtime, so changes need no extra step. `dotnet publish` runs `codegen write` and compiles the
  generated code in, and Production loads it ready-made (`TypeLoadMode.Static`): a faster start,
  no runtime compilation, and a clear error at startup if anything is missing. The generated
  code is never committed (`Internal/Generated/` is ignored), so it can't go stale.
- An OpenAPI-generated, fully-typed TypeScript client (`openapi-typescript` + `openapi-fetch`) —
  regenerate after changing API contracts with `npm run gen:api` (requires the API running).
- (`--Identity true`) JWT access tokens (HS256, 15 min) + rotating refresh tokens (14 days) with
  reuse-detection (a previously-rotated-away token being replayed revokes the whole chain).
  Client-side tokens are held only in memory (not `sessionStorage`/`localStorage`) to avoid
  exposing them to an injected script — the trade-off is that a hard page reload forces re-login.
  The Todo endpoints require login and each user sees only their own Todos. The dev signing key
  lives in `appsettings.Development.json` only; outside Development, set `Jwt:SigningKey` (≥ 32
  bytes) via user-secrets, the `Jwt__SigningKey` environment variable, or a secret store — startup
  fails if it's missing or still the dev key.
- (`--IncludeTests true`, the default) A `tests/` project (xUnit + [Alba](https://jasperfx.github.io/alba/)
  + Testcontainers) that boots the real app against a throwaway Postgres container and exercises
  the Todo endpoints, validation, (using Wolverine's message tracking) the background
  `TodoCompleted` handler, and SignalR notifications received by real hub clients, plus (when `--Identity true`) register/login/refresh-rotation/
  reuse-detection/logout. It drives the app over HTTP, so one project covers both `--Persistence`
  choices; the Marten version adds a test of the Todo event stream.

### Known limitations

- **HS256 vs RS256/JWKS**: the JWT signing key is a single shared secret (HS256). This is
  sufficient if this API and its own bundled React client are the only parties validating
  tokens. If you actually need a *separate* service to verify tokens without sharing that
  secret, upgrade to asymmetric signing (RS256) with a `/.well-known/jwks.json` endpoint —
  not included here to keep the default lean.

## Status

CI generates and builds all 8 parameter combinations on every push, and runs the integration
tests against a real Postgres (Testcontainers). The app has also been checked by hand in the
browser (Marten + Identity), including realtime updates across two tabs. See
[ROADMAP.md](ROADMAP.md) for what's next, starting with the first nuget.org release.
