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
- **Node.js 22.22 or later** (React Router 8 needs it), with npm.
- **Docker with the Compose plugin.** Postgres runs in Docker for development, and the tests
  start their own throwaway Postgres container. Check with `docker compose version`. On
  Ubuntu/Debian/Mint, the `docker.io` package doesn't include Compose: also install
  `docker-compose-v2`, and add yourself to the `docker` group
  (`sudo usermod -aG docker $USER`, then log out and back in).

## Install

The template is on nuget.org as
[LibreSharp.Templates.WolverineFxReact](https://www.nuget.org/packages/LibreSharp.Templates.WolverineFxReact).
Install it once, then create projects with `dotnet new wolverine-react`:

```bash
dotnet new install LibreSharp.Templates.WolverineFxReact
```

| To | Run |
|---|---|
| Update to the latest version | `dotnet new update` |
| Install a specific version | `dotnet new install LibreSharp.Templates.WolverineFxReact::0.1.1` |
| See the template's options | `dotnet new wolverine-react --help` |
| Uninstall | `dotnet new uninstall LibreSharp.Templates.WolverineFxReact` |

Updating the template only affects projects you create afterwards.

To try unreleased changes, install from a clone of this repository instead:
`dotnet new install ./` (run `dotnet new uninstall ./` to remove it).

## Usage

```bash
dotnet new wolverine-react -n MyApp
cd MyApp
docker compose up -d          # starts Postgres (and Mailpit, with --Identity true)
cd src/Client && npm install
cd ../MyApp && dotnet run      # API on the port shown; Vite dev server proxies /api to it
```

In a separate terminal:

```bash
cd MyApp/src/Client
npm run dev                    # http://localhost:5173, proxies /api/* to the API
```

With `--Identity true`, a demo user (`demo@example.com` / `Demo123!`) is created at startup in
Development only. It's already confirmed and is an admin, and the dev server pre-fills the login
form with it. Change it under `DevelopmentUser` in `appsettings.Development.json` and in
`src/Client/.env.development`. Emails the app sends in Development (confirmation, password reset,
invitations) land in Mailpit at http://localhost:8025.

### Parameters

| Parameter | Values | Default | Effect |
|---|---|---|---|
| `--Persistence` | `marten` \| `efcore` | `marten` | Document DB/event sourcing over PostgreSQL (Marten) vs. relational EF Core over PostgreSQL |
| `--Identity` | `true` \| `false` | `false` | Adds accounts: ASP.NET Core Identity with JWT tokens, email confirmation, account settings, lockout, roles and an admin page, invitations, Google/GitHub sign-in and passkeys |
| `--IncludeTests` | `true` \| `false` | `true` | Includes the `tests/` project(s) |

Example:

```bash
dotnet new wolverine-react -n MyApp --Persistence efcore --Identity true
```

## What you get

### Backend

- **Wolverine HTTP endpoints in vertical slices.** `Features/Todos` is a worked example
  (`[WolverineGet]`/`[WolverinePost]`) on either Marten or EF Core, showing what Wolverine adds
  beyond plain endpoints:
  - **Messaging with a transactional outbox.** Completing a Todo cascades a `TodoCompleted`
    message to a background handler. Local queues are durable, so the message is saved in the
    same database transaction as the change: it survives a restart and is never sent if the
    change rolls back.
  - **Scheduled messages.** "Remind me…" schedules a durable message that pushes a reminder to
    the browser when it's due, unless the Todo was completed first.
  - **Event sourcing** (`--Persistence marten`). Each Todo is an event stream; the complete
    endpoint uses Wolverine's aggregate handler workflow (`[WriteAggregate]`) with an optimistic
    concurrency check, and an inline snapshot projection keeps a queryable document in step.
  - **Validation.** FluentValidation rejects bad input with a 400 `ProblemDetails` response
    before the endpoint runs; the React forms show the messages.
  - **Realtime updates with SignalR** through Wolverine's SignalR transport: every open tab
    refreshes. With `--Identity true`, updates go only to the signed-in user's own tabs.
- **Production-ready code generation.** Development generates Wolverine's code at runtime;
  `dotnet publish` runs `codegen write` and Production loads it ready-made
  (`TypeLoadMode.Static`): a faster start, no runtime compilation, and a clear error at startup if
  anything is missing. The generated code is never committed, so it can't go stale.
- **Health checks** (`/health/live`, and `/health/ready` with a PostgreSQL check) and
  **OpenTelemetry** traces, metrics and logs from ASP.NET Core, Npgsql, Wolverine and Marten,
  exported over OTLP when configured, with an optional Aspire dashboard in `docker-compose`.
- **An API reference page** in Development ([Scalar](https://scalar.com/) at `/scalar`) and a
  `.http` file with the app's requests.

### Frontend

- **React, Vite, TypeScript and Tailwind CSS**, with an app shell (header, user menu, home page,
  footer, not-found page).
- **React Router** with real URLs; with `--Identity true`, signed-out users are sent to `/login`
  and back to the page they wanted.
- **TanStack Query** for server data, invalidated by mutations and SignalR messages, and cleared
  on logout so the next user never sees the previous user's data.
- **A typed API client** generated from the OpenAPI document (`openapi-typescript` +
  `openapi-fetch`); regenerate with `npm run gen:api`.
- **One deployable:** the Vite dev server proxies `/api` in development (no CORS), and in
  production the API serves the built client.

### Accounts (`--Identity true`)

- **ASP.NET Core Identity with JWT:** short-lived access tokens and rotating refresh tokens with
  reuse detection, kept in memory in the browser. Each user sees only their own data.
- **Account emails:** email confirmation (required to log in), confirmed email changes, password
  reset and invitations, sent with [MailKit](https://github.com/jstedfast/MailKit) from a
  Wolverine handler, so they go through the durable outbox and are retried. In Development,
  [Mailpit](https://github.com/axllent/mailpit) catches them.
- **Account settings:** display name, email (after confirming the new one), password, passkeys,
  linked Google/GitHub accounts, and account deletion (which deletes the user's data through an
  `AccountDeleted` message each feature handles).
- **Roles and an admin page** (`/admin`): search users, change roles, lock and unlock accounts,
  and invite people by email. The first admins come from configuration, and registration can be
  made invitation-only.
- **Sign-in with Google and GitHub**, each turned on by setting its client ID and secret.
- **Passkeys** with ASP.NET Core Identity's own WebAuthn support: add them in Settings, then log
  in without a password.
- **Account lockout** with Identity's lockout, which also stops refresh tokens.

### Security

- Browser security headers on every response (a strict Content-Security-Policy, framing, MIME
  sniffing and referrer protections), HTTPS redirection and HSTS in production, and
  development-only tools kept out of production.
- With `--Identity true`: verified emails, one-time and short-lived email links whose tokens are
  never stored, no account enumeration from "forgot password", rate limiting on sign-in and
  account endpoints, lockout, sessions ended on password changes and admin locks, verified-email
  rules and pre-hijacking protection for Google/GitHub sign-in, and a startup check that refuses
  a weak or development signing key in production.
- The generated app's README has a Security section describing each measure and what to do when
  deploying.

### Deployment

- **Container publishing** (`dotnet publish /t:PublishContainer`) and a production
  `docker-compose` with PostgreSQL and [Caddy](https://caddyserver.com/) for automatic HTTPS,
  with a step-by-step guide for a small VPS (`deploy/`).
- **Data Protection keys in PostgreSQL**, so email links and other protected tokens work across
  restarts and instances.

### Tests (`--IncludeTests true`, the default)

- A `tests/` project (xUnit, [Alba](https://jasperfx.github.io/alba/) and Testcontainers) that
  boots the real app against a throwaway Postgres and drives it over HTTP, so one project covers
  both `--Persistence` choices. It covers the Todo endpoints and validation, background and
  scheduled messages (with Wolverine's message tracking), SignalR with real hub clients, health
  checks, security headers, and the Marten event stream.
- With `--Identity true`: registration and email confirmation (following the links in captured
  emails), login, token rotation and reuse detection, account settings, password reset, lockout,
  rate limiting, roles and the admin endpoints, invitations, Google/GitHub sign-in rules, and
  passkeys (with a software authenticator, so Identity's real WebAuthn checks run).

### Developer experience

- **A README for the generated app** (`README.app.md`, renamed to `README.md`): running it, the
  project layout, how the example works, adding a feature, common tasks, deployment and
  security, matching the chosen options.
- **Help for AI coding assistants:** `AGENTS.md` (with `CLAUDE.md` importing it) describes the
  app's conventions and the rules that are easy to miss.
- **Housekeeping:** central package management (`Directory.Packages.props`), the free Meziantou
  analyzer with a commented `.editorconfig` (0 warnings), and Dependabot for NuGet, npm and Docker.

### Known limitations

- **HS256 vs RS256/JWKS**: the JWT signing key is a single shared secret (HS256). This is
  sufficient if this API and its own bundled React client are the only parties validating
  tokens. If you actually need a *separate* service to verify tokens without sharing that
  secret, upgrade to asymmetric signing (RS256) with a `/.well-known/jwks.json` endpoint —
  not included here to keep the default lean.
- **Reloading the page signs you out** (`--Identity true`): tokens are kept in memory, not browser
  storage, which is safer against injected scripts. A `sessionStorage` option is on the roadmap
  if people ask.
- **No two-factor codes:** passkeys are the stronger alternative offered; TOTP authenticator
  codes aren't included.
- **Passkeys belong to one domain:** they're created for the site's host name, so moving the app
  to another domain means users add new passkeys.
- **Role changes apply at the next token refresh** (within the access token's lifetime), and an
  admin lock ends sessions the same way.

## Releasing

Push a version tag; `.github/workflows/release.yml` runs the full CI matrix, then packs the
template with that version, pushes it to nuget.org and creates a GitHub release:

```bash
git tag v0.1.0 && git push origin v0.1.0     # a tag with a hyphen (v0.2.0-beta.1) is a prerelease
```

One-time setup, so the workflow can publish without a stored API key (nuget.org Trusted
Publishing):

1. On nuget.org, under your account's **Trusted Publishing** settings, add a GitHub Actions
   policy for owner `LibreSharp`, repository `WolverineFx-React`, workflow file `release.yml`.
2. In this GitHub repository, add the **variable** (not secret) `NUGET_USER`, set to that
   nuget.org account's username.

The nuget.org page shows `nuget/README.md`; this README is for working on the template.


CI generates all 8 parameter combinations on every push: it builds them, runs the integration
tests against a real Postgres (Testcontainers), builds the container image, and publishes and
starts each one in Production. See [ROADMAP.md](ROADMAP.md) for what's next.
