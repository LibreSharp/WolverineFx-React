# LibreSharp WolverineFx + React Template

A `dotnet new` template pairing a [WolverineFx](https://wolverinefx.net/) backend (HTTP
endpoints + message bus) with a React ([Vite](https://vite.dev/) + TypeScript + Tailwind CSS +
[shadcn/ui](https://ui.shadcn.com/)) frontend, built as a single deployable ASP.NET Core host.

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
| Install a specific version | `dotnet new install LibreSharp.Templates.WolverineFxReact@0.4.1` |
| See the template's options | `dotnet new wolverine-react --help` |
| Uninstall | `dotnet new uninstall LibreSharp.Templates.WolverineFxReact` |

Updating the template only affects projects you create afterwards.

To try unreleased changes, install from a clone of this repository instead:
`dotnet new install ./` (run `dotnet new uninstall ./` to remove it).

### Changes in 0.4.1

- **Billing and a back office** with `--Auth saas`: plans, a reverse trial, limits and Stripe
  subscriptions, and admin pages for organisations, feature flags, an audit log and failed
  messages. The pricing page is public and pre-rendered.
- **shadcn/ui and dark mode:** every page is built from shadcn/ui components and the theme's
  colours, and follows the operating system's light or dark setting.
- **With accounts, the admin page is now a back office** with sections (`/admin/...`), and the
  user menu is a dropdown.

### Changes in 0.4.0

- **`--Identity` is replaced by `--Auth`:** use `--Auth identity` for `--Identity true` and
  `--Auth none` for `--Identity false`. The new `--Auth saas` adds organisations.
- **With accounts, production needs `App__ClientUrl`:** the app's public address, such as
  `https://app.example.com`. Links in emails are built from it, never from the request's host,
  and the app won't start without it outside Development. The production `docker-compose` sets
  it from `DOMAIN`.
- The public pages are pre-rendered (React Router's framework mode), account endpoints moved to
  `Features/Auth`, `Features/Account` and `Features/Admin`, and code that differs between Marten
  and EF Core is in `Name.Marten.cs` / `Name.EfCore.cs` files.

## Usage

```bash
dotnet new wolverine-react -n MyApp
cd MyApp
docker compose up -d          # starts Postgres (and Mailpit, with accounts)
cd src/Client && npm install
cd ../MyApp && dotnet run      # API on the port shown; Vite dev server proxies /api to it
```

In a separate terminal:

```bash
cd MyApp/src/Client
npm run dev                    # http://localhost:5173, proxies /api/* to the API
```

With accounts (`--Auth identity` or `saas`), a demo user (`demo@example.com` / `Demo123!`) is created at startup in
Development only. It's already confirmed and is an admin, and the dev server pre-fills the login
form with it. Change it under `DevelopmentUser` in `appsettings.Development.json` and in
`src/Client/.env.development`. Emails the app sends in Development (confirmation, password reset,
invitations) land in Mailpit at http://localhost:8025.

### Parameters

| Parameter | Values | Default | Effect |
|---|---|---|---|
| `--Persistence` | `marten` \| `efcore` | `marten` | Document DB/event sourcing over PostgreSQL (Marten) vs. relational EF Core over PostgreSQL |
| `--Auth` | `none` \| `identity` \| `saas` | `none` | `identity` adds accounts: ASP.NET Core Identity with JWT tokens, email confirmation, account settings, lockout, roles, a back office (users, audit log, failed messages), invitations, Google/GitHub sign-in and passkeys. `saas` adds organisations on top: data belongs to an organisation, with members, roles (Owner, Admin, Member), email invitations and a switcher; plans, trials, limits and Stripe billing; and a back office for organisations and feature flags |
| `--IncludeTests` | `true` \| `false` | `true` | Includes the `tests/` project(s) |

Example:

```bash
dotnet new wolverine-react -n MyApp --Persistence efcore --Auth saas
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
    refreshes. With `--Auth identity`, updates go only to the signed-in user's own tabs; with
    `saas`, to everyone working in the organisation, and reminders only to whoever set them.
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

- **React, Vite, TypeScript, Tailwind CSS and [shadcn/ui](https://ui.shadcn.com/)** (MIT), with
  an app shell (header, user menu, home page, footer, not-found page). Every page is built from
  shadcn/ui components (they're the app's own files, in `components/ui`) and the theme's
  colours, with **dark mode** that follows the operating system and needs no script.
- **React Router** (framework mode, as a single-page app) with real URLs; with accounts,
  signed-out users are sent to `/login` and back to the page they wanted.
- **Pre-rendered public pages:** the home page is real HTML at build time, so search engines and
  link previews see its content without JavaScript, and a CDN can cache it. Unknown paths are
  real 404s, private pages are marked `noindex`, and the API serves `robots.txt` and
  `sitemap.xml`. Still one deployable, with no Node server.
- **TanStack Query** for server data, invalidated by mutations and SignalR messages, and cleared
  on logout so the next user never sees the previous user's data.
- **A typed API client** generated from the OpenAPI document (`openapi-typescript` +
  `openapi-fetch`); regenerate with `npm run gen:api`.
- **One deployable:** the Vite dev server proxies `/api` in development (no CORS), and in
  production the API serves the built client.

### Accounts (`--Auth identity` or `saas`)

- **ASP.NET Core Identity with JWT:** short-lived access tokens and rotating refresh tokens with
  reuse detection, kept in memory in the browser. With `identity`, each user sees only their own
  data; with `saas`, their organisation's.
- **Account emails:** email confirmation (required to log in), confirmed email changes, password
  reset and invitations, sent with [MailKit](https://github.com/jstedfast/MailKit) from a
  Wolverine handler, so they go through the durable outbox and are retried. In Development,
  [Mailpit](https://github.com/axllent/mailpit) catches them.
- **Account settings:** display name, email (after confirming the new one), password, passkeys,
  linked Google/GitHub accounts, and account deletion (which deletes the user's data through an
  `AccountDeleted` message each feature handles).
- **Roles and a back office** (`/admin`): search users, change roles, lock and unlock accounts,
  end a user's sessions, resend confirmation emails, and invite people by email; an **audit log**
  of every admin action; and **failed messages** (Wolverine's dead-letter queue) to replay or
  discard. The first admins come from configuration, and registration can be made
  invitation-only.
- **Sign-in with Google and GitHub**, each turned on by setting its client ID and secret.
- **Passkeys** with ASP.NET Core Identity's own WebAuthn support: add them in Settings, then log
  in without a password.
- **Account lockout** with Identity's lockout, which also stops refresh tokens.

### Organisations (`--Auth saas`)

- **Multi-tenancy with Wolverine:** data belongs to organisations. The access token names the
  organisation the user works in, Wolverine reads it as each request's tenant id and carries it
  through messages, and Marten's (or Wolverine's EF Core) conjoined tenancy keeps each
  organisation's data apart, with no filters to forget in endpoints.
- **Every account gets its own organisation;** users create more, are invited into others, and
  switch between them in the header. Removing a member takes effect at once.
- **Roles:** Owner, Admin and Member, with at least one owner always.
- **Invitations by email:** existing users accept after logging in; new users create their
  account from the link, which confirms their email. Invitations are limited per user and per
  organisation, so the app can't be used to send spam.
- **Deleting an organisation** deletes its data through an `OrganisationDeleted` message that
  each feature handles for that tenant, published in the same transaction as the deletion.

### Billing (`--Auth saas`)

- **A plan catalogue** in one place (`Billing/Plans.cs`): prices per currency, features and
  limits, with versions so existing customers keep their price ("grandfathering"). The public
  **pricing page** is pre-rendered from it.
- **A reverse trial:** new organisations get the top plan for 14 days, then the free plan.
- **Entitlements:** one check for "can this organisation use feature X?" and "how much of limit
  Y is left?", in Wolverine `Before` methods (402 Payment Required) and in the client to show or
  hide features and upgrade prompts. Features are feature flags
  ([Microsoft.FeatureManagement](https://github.com/microsoft/FeatureManagement-Dotnet), MIT).
  Limits count what exists (open Todos, members) or use per billing period (reminders a month),
  with a warning at 80%. A downgrade never deletes data.
- **Stripe subscriptions** ([Stripe.net](https://github.com/stripe/stripe-dotnet), Apache-2.0):
  Checkout and the customer portal; signed webhooks stored once and handled from a durable
  Wolverine queue, keeping a local copy of billing state so the app never asks Stripe which plan
  an organisation is on; and `dotnet run -- stripe-prices` to create the prices from the
  catalogue. Payments sit behind `IPaymentProvider`, so another provider can replace Stripe.
- **A back office for customers:** a dashboard (monthly recurring revenue, organisations,
  trials ending, failed payments), organisations (members, plan, usage, invoices with Stripe
  links; give a plan until a date or extend a trial, suspend, notes, delete) and feature flags
  for everyone, a plan or one organisation.

### Security

- Browser security headers on every response (a strict Content-Security-Policy that allows only
  the build's own inline start-up scripts, by hash; framing, MIME sniffing and referrer
  protections), HTTPS redirection and HSTS in production, and development-only tools kept out of
  production.
- With accounts: verified emails, one-time and short-lived email links whose tokens are
  never stored, no account enumeration from "forgot password", rate limiting on sign-in and
  account endpoints, lockout, sessions ended on password changes and admin locks, verified-email
  rules and pre-hijacking protection for Google/GitHub sign-in, email links built from the
  configured address (never the request's host), realtime connections closed when their token
  expires, and startup checks that refuse a weak or development signing key, or a missing
  `App:ClientUrl`, in production.
- The generated app's `docs/security.md` describes each measure and what to do when deploying.

### Deployment

- **Container publishing** (`dotnet publish /t:PublishContainer`) and a production
  `docker-compose` with PostgreSQL and [Caddy](https://caddyserver.com/) for automatic HTTPS,
  with a step-by-step guide for a small VPS (`deploy/`).
- **Data Protection keys in PostgreSQL**, so email links and other protected tokens work across
  restarts and instances.
- The generated `docs/deployment.md` lists every production setting.

### Tests

- **Client unit tests** (always included): Vitest and React Testing Library with the API faked
  by [MSW](https://mswjs.io/), next to the code they test, covering the Todo page and, with
  accounts, the token refresh, every sign-in page, route guards, Settings and the admin
  page, and with `saas` the organisation switcher, the organisation page, the join page, the
  pricing page, plans and limits, and the back office.

With `--IncludeTests true` (the default):

- **End-to-end tests** with [Playwright](https://playwright.dev/) (Apache-2.0) in the installed
  Chrome, against the running app: Todos with live updates, the pre-rendered pages and search
  engine files and, with accounts, sign-up with the real confirmation email (read from Mailpit),
  settings, password reset, lockout, the admin page and invitations, passkeys with Chrome's
  virtual authenticator, and (with `saas`) a teammate joining from an invitation email and an
  admin giving an organisation a plan in the back office. CI runs
  them against each published combination.

- A `tests/` project (xUnit, [Alba](https://jasperfx.github.io/alba/) and Testcontainers) that
  boots the real app against a throwaway Postgres and drives it over HTTP, so one project covers
  both `--Persistence` choices. It covers the Todo endpoints and validation, background and
  scheduled messages (with Wolverine's message tracking), SignalR with real hub clients, health
  checks, security headers, and the Marten event stream.
- With accounts: registration and email confirmation (following the links in captured
  emails), login, token rotation and reuse detection, account settings, password reset, lockout,
  rate limiting, roles and the admin endpoints (with the audit log and failed messages),
  invitations, Google/GitHub sign-in rules, and passkeys (with a software authenticator, so
  Identity's real WebAuthn checks run).
- With `saas`: organisations kept apart, switching, roles and the always-an-owner rule (including
  changes at the same moment), removal taking effect at once, invitations and their limits,
  deleting an organisation's data, and organisation-wide realtime updates; trials, features and
  limits (including requests at the same moment), downgrades, checkout and webhooks with a fake
  payment provider, Stripe's signature checks, and the back office (suspension, feature flags,
  replaying a failed message).

### Developer experience

- **Documentation for the generated app:** a short README (running it, the project layout,
  common tasks) and a `docs/` folder (from `docs.app/` here) with architecture, accounts,
  security, testing, observability and deployment pages, plus how-to guides: add a feature,
  put Cloudflare in front, change the database schema, send email through your provider, set up
  Google/GitHub sign-in, add a role, make someone an admin, and change the JWT signing key. Each
  app gets only the pages that match its options.
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
- **Reloading the page signs you out** (with accounts): tokens are kept in memory, not browser
  storage, which is safer against injected scripts. A `sessionStorage` option is on the roadmap
  if people ask.
- **No two-factor codes:** passkeys are the stronger alternative offered; TOTP authenticator
  codes aren't included.
- **Passkeys belong to one domain:** they're created for the site's host name, so moving the app
  to another domain means users add new passkeys.
- **Role changes apply at the next token refresh** (within the access token's lifetime), and an
  admin lock ends sessions the same way.

## Contributing and releasing

- [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md): how the template is laid out, conditional code,
  testing a change, regenerating the client's API types, and EF Core migrations.
- [docs/releasing.md](docs/releasing.md): publishing a version to nuget.org.
- [docs/decisions.md](docs/decisions.md): why things are the way they are.

The nuget.org page shows `nuget/README.md`; this README is for working on the template.

CI generates all 12 parameter combinations on every push: it builds them, runs the integration
tests against a real Postgres (Testcontainers) and the client's unit tests, builds the container
image, runs the end-to-end tests against the published app, and publishes and
starts each one in Production. See [ROADMAP.md](ROADMAP.md) for what's next.
