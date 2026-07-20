# WolverineFx + React Template

A `dotnet new` template pairing a [WolverineFx](https://wolverinefx.net/) backend (HTTP
endpoints + message bus) with a React ([Vite](https://vite.dev/) + TypeScript + Tailwind CSS)
frontend, built as a single deployable ASP.NET Core host.

## Install

```bash
dotnet new install WolverineFxReact.Templates
```

(Package not yet published — see "Open decisions" below.)

## Usage

```bash
dotnet new wolverine-react -n MyApp
cd MyApp
docker compose up -d          # starts Postgres
cd src/Client && npm install
cd ../Api && dotnet run        # API on the port shown; Vite dev server proxies /api to it
```

In a separate terminal:

```bash
cd MyApp/src/Client
npm run dev                    # http://localhost:5173, proxies /api/* to the API
```

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
  (`[WolverineGet]`/`[WolverinePost]`) wired to either Marten or EF Core.
- Vite dev server proxies `/api/*` to the API in development (no CORS); in production, the
  client is built and served as static files from the same Kestrel host (`dotnet publish` builds
  the client automatically).
- An OpenAPI-generated, fully-typed TypeScript client (`openapi-typescript` + `openapi-fetch`) —
  regenerate after changing API contracts with `npm run gen:api` (requires the API running).
- (`--Identity true`) JWT access tokens (HS256, 15 min) + rotating refresh tokens (14 days) with
  reuse-detection (a previously-rotated-away token being replayed revokes the whole chain).
  Client-side tokens are held only in memory (not `sessionStorage`/`localStorage`) to avoid
  exposing them to an injected script — the trade-off is that a hard page reload forces re-login.

### Known limitations

- **HS256 vs RS256/JWKS**: the JWT signing key is a single shared secret (HS256). This is
  sufficient if this API and its own bundled React client are the only parties validating
  tokens. If you actually need a *separate* service to verify tokens without sharing that
  secret, upgrade to asymmetric signing (RS256) with a `/.well-known/jwks.json` endpoint —
  not included here to keep the default lean.
- **Dev-time code generation**: Wolverine's dynamic codegen (`WolverineFx.RuntimeCompilation`)
  is used in both dev and prod right now. For production, consider switching to
  `TypeLoadMode.Static` with pre-generated code (`dotnet run -- codegen write`) to drop the
  Roslyn dependency and cut startup time.
- Full CRUD and login/refresh runtime verification was done at the wiring level (reaches
  Postgres, fails only on connection-refused) rather than against a live database, since Docker
  wasn't available in the environment this template was built in. Run `docker compose up -d`
  and exercise the endpoints before trusting this in anger.

## Open decisions

- **Package id / publishing**: currently packaged as `WolverineFxReact.Templates` (placeholder).
  Decide whether to self-publish under your own NuGet id, or pursue upstreaming into
  WolverineFx's own repo (a different, higher-bar path — see `SPEC.md` §7).

## What's left

See `SPEC.md` §11 ("Remaining work") for the full punch list. Top of it: nothing has been
exercised against a live Postgres yet (`docker compose up -d` + a manual pass through
register/login/CRUD/refresh/logout across all 8 parameter combinations) — that's the one gap
that could still hide a real bug.

See `SPEC.md` for the full design history and rationale.
