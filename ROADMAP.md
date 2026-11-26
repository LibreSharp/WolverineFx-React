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
- **Frontend:** React, Vite, TypeScript and Tailwind CSS, with an API client generated from the
  OpenAPI document. Single deployable: Kestrel serves the built client in production.
- **Tests** (`--IncludeTests true`): xUnit, Alba and Testcontainers against a real Postgres,
  covering the endpoints, auth, messaging, the event stream and SignalR.
- **CI:** all 8 option combinations on every push: build, tests, and a Production publish
  that is started and called.

## Next: v0.1.0 on nuget.org

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
- [ ] **One-time nuget.org setup** (needs the account owner): a Trusted Publishing policy for
      `release.yml`, and the `NUGET_USER` repository variable. See "Releasing" in the README.
- [ ] **Tag `v0.1.0`.**
- [x] **Confirm every dependency's licence:** all MIT, Apache-2.0 or PostgreSQL (checked
      November 2026). `Marten.AspNetIdentity` states MIT only in its GitHub repo, not in its
      package metadata, so licence scanners report it as unlicensed; worth raising with its author.
- [ ] **Reserve the `LibreSharp.` ID prefix** on nuget.org after the first package is out.
- [ ] **List the template** on the LibreSharp site and in the `dotnet/templating` wiki's list
      of available templates.

## Then: frontend

- [ ] **TanStack Query** for data fetching and caching; SignalR messages invalidate the cache.
- [ ] **React Router** for real URLs on the login and Todo pages.
- [ ] **daisyUI** components on top of Tailwind, including dark mode.

## Later, if users ask

- [ ] **.NET Aspire option:** an app host that starts Postgres, the API and Vite together, in
      place of Docker Compose.
- [ ] **Container publishing option** (`dotnet publish /t:PublishContainer`).
- [ ] **RS256 signing with a JWKS endpoint,** for when a separate service must verify tokens
      without sharing the signing secret. HS256 is enough for this API and its own client.
- [ ] **`sessionStorage` token option,** so logins survive a page reload. Tokens are kept in
      memory today, which is safer against injected scripts.

## Upgrades to track

- WolverineFx 7 and Marten 10 once stable (both are release candidates as of November 2026).
- xUnit v3, Alba 9, TypeScript 7, and the current Node LTS in CI.

## Decided against

- **A SharedWorker to share one SignalR connection across tabs.** One connection per tab is
  cheap, and a worker would need access to tokens that are deliberately kept per tab.
  `BroadcastChannel` is the lighter route if one connection per browser is ever needed.
- **Loading the CSS framework from a CDN.** Vite builds only the classes the app uses, pins the
  version, and avoids a runtime third-party dependency.
- **Handing the template to the WolverineFx maintainers.** It is published independently by
  LibreSharp as a community template; that can be revisited if they are interested.
