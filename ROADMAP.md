# Roadmap

Where the LibreSharp WolverineFx + React template is heading. Suggestions are welcome as GitHub
issues.

## Principles

- **Free, open-source dependencies only.** The template exists to show a stack with no
  commercial licences, in the spirit of [LibreSharp](https://libresharp.github.io).
- **Few template options.** Each yes/no option doubles the combinations CI must build and test,
  so new options wait for real demand. Features that fit every project go in without an option.
- **Every combination is tested.** CI generates, builds and tests all option combinations
  against a real Postgres on every push.
- **Generated apps are the user's own.** No template branding in names, and nothing that only
  makes sense inside this repo.

## Shipped

- **Backend:** .NET 11, Wolverine HTTP endpoints in vertical slices, PostgreSQL via
  Marten (`--Persistence marten`, the default) or EF Core (`--Persistence efcore`).
- **Messaging:** cascaded messages, a transactional outbox and durable local queues
  (`TodoCompleted` handled in the background).
- **Event sourcing** (Marten): Todo event streams, Wolverine's aggregate handler workflow, inline
  snapshot projection.
- **Validation:** FluentValidation middleware returning `ProblemDetails`, shown in the UI.
- **Realtime:** SignalR through Wolverine's transport; every open tab updates, scoped to the
  signed-in user when Identity is on.
- **Identity** (`--Identity true`): JWT access tokens with rotating refresh tokens and reuse
  detection, per-user Todos, and a Development-only demo login.
- **Frontend:** React, Vite, TypeScript and Tailwind CSS, React Router and TanStack Query, with an
  API client generated from the OpenAPI document. Single deployable: Kestrel serves the built client in production.
- **Tests** (`--IncludeTests true`): xUnit, Alba and Testcontainers against a real Postgres,
  covering the endpoints, auth, messaging, the event stream and SignalR.
- **Documentation in every generated app:** a README covering running, layout, patterns,
  adding a feature, common tasks and deployment, tailored to the chosen options.
- **Requests to try in your IDE:** a `.http` file with the app's requests (login first when
  Identity is on), each reusing earlier responses such as the new Todo's id.
- **CI:** all 8 option combinations on every push: build, tests, and a Production publish
  that is started and called.

## v0.1.0 on nuget.org (released 26 November 2026)

- [x] **Pre-generated Wolverine code for production:** `dotnet publish` runs `codegen write`
      and Production loads the result (`TypeLoadMode.Static`), so no code is compiled at
      runtime and missing code fails at startup. CI publishes every combination and runs it in
      Production. (Development still compiles at runtime, so the Roslyn package and the EF Core
      option's `Microsoft.CodeAnalysis` pin stay.)
- [x] **NuGet readme** (`nuget/README.md`, packed as `PackageReadmeFile`) written for people
      browsing nuget.org.
- [x] **Publish from CI** when a `v*` tag is pushed (`release.yml`): runs the full CI matrix,
      then publishes with nuget.org Trusted Publishing (no stored API key) and creates a GitHub
      release.
- [x] **One-time nuget.org setup:** a Trusted Publishing policy for `release.yml`, and the
      `NUGET_USER` repository variable. See "Releasing" in the README.
- [x] **Tag `v0.1.0`:** published as
      [LibreSharp.Templates.WolverineFxReact](https://www.nuget.org/packages/LibreSharp.Templates.WolverineFxReact).
- [x] **Confirm every dependency's licence:** all MIT, Apache-2.0 or PostgreSQL (checked
      November 2026). `Marten.AspNetIdentity` states MIT only in its GitHub repo, not in its
      package metadata, so licence scanners report it as unlicensed; worth raising with its author.
- [ ] **Reserve the `LibreSharp.` ID prefix** on nuget.org after the first package is out.
- [x] **List the template** on the [LibreSharp site](https://libresharp.github.io/templates/#libresharp-wolverinefx-react)
      (26 November 2026).
- [ ] **List the template** in the `dotnet/templating` wiki's list of available templates.

## Then: frontend

- [x] **TanStack Query** for data fetching and caching; SignalR messages invalidate the cache,
      and logout clears it.
- [x] **React Router** (v8) for real URLs on the login, register and Todo pages.

## Next: a comprehensive free SaaS template

The goal of the next phases is a template that covers everything paid .NET starter kits sell,
on free, open-source packages, and that deploys to cheap hosting. No free template does this
today: PlatformPlatform has Stripe billing but deploys only to Azure, and fullstackhero has
billing records but no payment provider (see the
[template comparison](https://libresharp.github.io/templates/)).

### v0.2: production-ready basics (no new options; CI stays at 8 combinations)

- [x] **App shell:** header navigation with a user menu, a public home page and a footer.
- [x] **Account settings** (Identity): change email and display name, change password, delete
      account (with an `AccountDeleted` message features handle to delete their data), and
      forgot/reset password by email.
- [x] **Container publishing and cheap hosting:** `dotnet publish /t:PublishContainer`, a
      production `docker-compose` with Postgres and [Caddy](https://caddyserver.com/)
      (Apache-2.0) for automatic HTTPS, and a deployment guide for a small VPS (`deploy/`). CI
      builds the image for every combination.
- [x] **Health checks:** `/health/live` and `/health/ready` (with a Postgres check,
      `AspNetCore.HealthChecks.NpgSql`, Apache-2.0); CI checks `/health/ready` on every published
      combination.
- [x] **OpenTelemetry** (Apache-2.0) traces, metrics and logs from ASP.NET Core, HTTP clients,
      Npgsql, Wolverine and Marten, exported over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is
      set; an optional Aspire dashboard in `docker-compose` (`--profile observability`).
- [x] **Persist ASP.NET Core Data Protection keys to Postgres,** so tokens survive container
      restarts and work with more than one instance (EF Core: Microsoft's package; Marten: a
      small `IXmlRepository` storing documents).
- [x] **Hardening:** rate limiting on the auth endpoints (built-in rate limiter, 10 a minute per
      client IP) and security headers (Content-Security-Policy, nosniff, frame and referrer
      policies, HSTS in production).
- [x] **API reference page** in Development with [Scalar](https://scalar.com/) (MIT) at
      `/scalar`, serving its scripts from the app rather than a CDN.
- [x] **Scheduled message example:** "Remind me…" on a Todo schedules a durable Wolverine message
      that pushes a reminder to the browser when due, unless the Todo was completed.
- [x] **Generated-app housekeeping:** an `AGENTS.md`/`CLAUDE.md` describing the app's patterns
      for AI coding assistants, Dependabot config (grouped updates), central package management
      (`Directory.Packages.props`), and the free Meziantou.Analyzer (0 warnings).

### v0.3: better accounts (inside `--Identity true`; still 8 combinations)

- [ ] **Email:** confirmation, password reset and invitations, sent through the Wolverine outbox
      so no email is lost on a restart. [MailKit](https://github.com/jstedfast/MailKit) (MIT) to
      send, and [Mailpit](https://github.com/axllent/mailpit) (MIT) in `docker-compose` to catch
      emails in development.
- [ ] **Roles and a small admin page:** list users, lock and unlock accounts, change roles.
- [ ] **External sign-in** with Google and GitHub.
- [ ] **Passkeys** with ASP.NET Core Identity (added in .NET 10; confirm the API first).
- [ ] **Account lockout** after repeated failed logins.

### v0.4: SaaS option

- [ ] **Replace `--Identity true|false` with `--Auth none|identity|saas`.** That gives
      2 × 3 × 2 = 12 CI combinations, instead of doubling for each new yes/no option.
- [ ] **Organisations (multi-tenancy)** in `saas`: Marten's built-in multi-tenancy or EF Core
      query filters, with Wolverine carrying the tenant ID through messages.
- [ ] **Team invitations** by email.
- [ ] **Stripe subscriptions:** Checkout and the customer portal, with Stripe webhooks turned
      into durable Wolverine messages (the outbox makes webhook handling reliable).
      [Stripe.net](https://github.com/stripe/stripe-dotnet) is open source (Apache-2.0); Stripe
      itself charges fees. Keep payments behind an interface so others can add Paddle or Lemon
      Squeezy, which handle sales tax and VAT.
- [ ] **Plan limits** enforced per organisation.
- [ ] **Admin back office:** organisations, users and subscriptions.

### Frontend quality (any time)

- [ ] **Frontend tests:** Vitest and React Testing Library for components, and
      [Playwright](https://playwright.dev/) end-to-end tests in CI covering login and realtime
      updates.
- [ ] **UI components:** [shadcn/ui](https://ui.shadcn.com/) (MIT) with dark mode, so admin and
      billing screens look finished.
- [ ] **Translations** with react-i18next (MIT), if users ask.

### Keep out

- JasperFx's paid tooling (for example CritterWatch) and commercial UI kits. Everything stays
  free and open source.

### Open decisions

- [ ] Should the `saas` option support both Marten and EF Core, or start Marten-only (simpler
      tenancy, fewer CI combinations)?
- [ ] Should email be on by default whenever Identity is on (Mailpit in development, SMTP
      settings in production)?
- [ ] Which cheap host should the deployment guide target first: a VPS with Docker Compose and
      Caddy, or Fly.io or Railway?

## Later, if users ask

- [ ] **.NET Aspire option:** an app host that starts Postgres, the API and Vite together, in
      place of Docker Compose.
- [ ] **RS256 signing with a JWKS endpoint,** for when a separate service must verify tokens
      without sharing the signing secret. HS256 is enough for this API and its own client.
- [ ] **`sessionStorage` token option,** so logins survive a page reload. Tokens are kept in
      memory today, which is safer against injected scripts.

## Upgrades to track

- WolverineFx 7 and Marten 10 once stable (both are release candidates as of November 2026).
- xUnit v3, Alba 9, TypeScript 7, and the current Node LTS in CI.

