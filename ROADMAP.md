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
  signed-in user with accounts, or to their organisation with `saas`.
- **Accounts** (`--Auth identity`): JWT access tokens with rotating refresh tokens and reuse
  detection, email confirmation, settings, lockout, roles and an admin page, Google/GitHub
  sign-in and passkeys, per-user Todos, and a Development-only demo login.
- **Organisations** (`--Auth saas`): multi-tenancy with Wolverine, roles, invitations and a
  switcher.
- **Frontend:** React, Vite, TypeScript and Tailwind CSS, React Router and TanStack Query, with an
  API client generated from the OpenAPI document, and pre-rendered public pages. Single
  deployable: Kestrel serves the built client in production.
- **Tests** (`--IncludeTests true`): xUnit, Alba and Testcontainers against a real Postgres,
  covering the endpoints, auth, organisations, messaging, the event stream and SignalR; and
  Playwright end-to-end tests against the published app.
- **Documentation in every generated app:** a README covering running, layout, patterns,
  adding a feature, common tasks and deployment, tailored to the chosen options.
- **Requests to try in your IDE:** a `.http` file with the app's requests (login first with
  accounts), each reusing earlier responses such as the new Todo's id.
- **CI:** all 12 option combinations (8 before v0.4.0) on every push: build, tests, and a Production publish
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

### v0.3: better accounts (inside `--Identity true`; still 8 combinations), released 27 November 2026

- [x] **Email:** confirmation (required to log in), confirmed email changes and password reset,
      sent through the Wolverine outbox so no email is lost on a restart, with the one-time
      token created when the email is sent so it's never stored.
      [MailKit](https://github.com/jstedfast/MailKit) (MIT) to send, and
      [Mailpit](https://github.com/axllent/mailpit) (MIT) in `docker-compose` to catch emails in
      development. Invitations come with the admin page.
- [x] **Roles and a small admin page:** list and search users, lock and unlock accounts, change
      roles (a fixed list in `AppRoles`, seeded to the role table), and invite people by email.
      The first admins come from `Accounts:AdminEmails`; `Accounts:AllowRegistration=false`
      makes accounts invitation-only. Marten's user store gained `IUserRoleStore`, so both
      persistence options use `UserManager`'s role methods.
- [x] **External sign-in** with Google and GitHub (Microsoft's Google handler, MIT, and
      AspNet.Security.OAuth.GitHub, Apache-2.0), each on only when configured. Only verified
      emails sign in or link (GitHub's primary email is checked against its email list); an
      unconfirmed account with that email loses its password first (pre-hijacking). The SPA
      gets a refresh token in the URL fragment and rotates it at once. Marten's user store
      gained `IUserLoginStore`.
- [x] **Passkeys** with ASP.NET Core Identity (added in .NET 10): `IPasskeyHandler` used
      directly, without SignInManager's cookie, by encrypting the ceremony state for the client.
      Add, rename and remove in Settings; log in with discoverable credentials. EF Core moves to
      Identity schema version 3 (an `AddPasskeys` migration); Marten's user store gained
      `IUserPasskeyStore`. Tests use a software authenticator, so Identity's real signature,
      challenge, origin and counter checks run.
- [x] **Account lockout** after repeated failed logins: Identity's own lockout (5 attempts,
      15 minutes) through `SignInManager.CheckPasswordSignInAsync`; refresh tokens stop working
      while locked, and a password reset lifts it.

### v0.3 wrap-up: documentation and test coverage (shipped in v0.3.0)

- [x] **Security section in the generated README:** every security measure described (sign-in,
      tokens, emails, lockout, rate limits, headers, roles, data protection, deployment) without
      details that would help an attacker, such as exact thresholds that aren't configurable.
- [x] **Template README brought fully up to date** with v0.3 (features, options, configuration,
      known limitations), and the NuGet readme to match.
- [x] **Backend test audit:** a test for every endpoint and every rule (success, validation,
      401/403/404, rate limiting, emails sent, lockout, roles, external sign-in, passkeys),
      in both persistence options; fill the gaps found. `EndpointSecurityTests` also checks
      every endpoint's metadata, so a new one that forgets `[Authorize]`, the Admin role or rate
      limiting fails. Not covered: the SMTP sender against a real server (checked by hand with
      Mailpit) and the provider round trip for Google/GitHub (needs real accounts).
- [x] **Frontend unit tests:** Vitest and React Testing Library for the auth, account, admin and
      Todo components and hooks (forms, error and notice messages, route guards, passkey and
      external sign-in pages), run in CI for every combination.
- [x] **End-to-end tests:** Playwright in CI against the published app and Mailpit: register,
      confirm by email, log in, Todos with realtime updates, settings, admin page, lockout.
      Passkeys with Chrome's virtual authenticator.

- [x] **A `docs/` folder in generated apps:** the README slimmed to an overview, with
      architecture, accounts, security, testing, observability and deployment pages and how-to
      guides, each included only when it matches the app's options.
- [x] **Contributor docs** in this repository's `docs/`: `CONTRIBUTING.md` (layout, conditional
      code, testing a change with `scripts/test-combination.sh`, regenerating the API types,
      EF Core migrations), `releasing.md` and `decisions.md`. CI now also checks that the
      packed template generates the same projects as this folder.

### v0.3.1: quick wins (no new options), released 27 November 2026

From an architecture and SEO review after v0.3.0 (November 2026).

- [x] **Errors as `ProblemDetails`:** `AddProblemDetails()` and `UseExceptionHandler()`, so an
      unhandled error in production returns a `ProblemDetails` body (with the trace ID, to match
      a user's report to the logs) rather than an empty 500.
- [x] **Lazy-loaded routes:** the client was one bundle of about 445 kB (133 kB gzipped),
      including the admin page, settings and SignalR, even for a visitor on the home page. Each
      page now loads when first visited; the first download is 374 kB (115 kB gzipped), most of
      it React itself.
- [x] **Page metadata:** a meta description, Open Graph and Twitter tags, a canonical link and
      `theme-color` in `index.html`, and per-page titles and descriptions with React 19's
      built-in `<title>`/`<meta>` support (no library).
- [x] **`robots.txt` and `sitemap.xml`:** everything may be crawled except `/api/` (blocking the
      private pages would stop crawlers seeing their `noindex`); the sitemap lists the pages
      marked `index` in `routes.ts`.
- [x] **`noindex` on pages that shouldn't be in search results:** login, register, the email-link
      pages, settings and admin.
- [x] **Real 404s:** unknown URLs return the app's HTML with status 200, which search engines treat
      as duplicate "soft 404" pages. Answer 404 for paths that aren't client routes (and add
      `noindex` to the not-found page).
- [x] **Say plainly that the app runs as one instance,** and what scaling out would need: today
      SignalR only reaches browsers connected to the instance that sent the message, and the
      sign-in rate limits are counted per instance. (Scheduled messages and the outbox already
      coordinate through the database.)
- [x] **Cloudflare in front, documented:** caching rules (cache the built assets and public pages,
      bypass `/api/`), "Full (strict)" TLS to Caddy, and Caddy trusting Cloudflare's addresses
      so the app sees each visitor's real IP. Without that, the sign-in rate limits count per
      Cloudflare server instead of per visitor.
- [x] **Record in `decisions.md`** that creating a user and storing its confirmation email aren't
      one transaction: a crash in between leaves an account without the email, which the user
      recovers with "Send the confirmation email again".

### v0.4.0: organisations (`--Auth none|identity|saas`), released 27 November 2026

First the groundwork, while there are few pages and 8 combinations, not 12:

- [x] **Persistence code in separate files per option,** not inline `#if (IsMarten)` blocks
      (`CreateTodo.cs` had four variants), so features stay readable at 12 combinations:
      `<Name>.Marten.cs` and `<Name>.EfCore.cs`, renamed to `<Name>.cs` in generated apps.
- [x] **Account endpoints as features:** `Features/Auth`, `Features/Account` and
      `Features/Admin` (and later `Features/Organisations`), keeping `Identity/` for what they
      share, to match the vertical slices the template teaches and the client's features.
- [x] **Pre-render the public pages** with React Router's framework mode (`ssr: false` and a
      `prerender` list): real HTML at build time for `/` (and later the SaaS landing and pricing
      pages), served as static files by the API and cacheable by a CDN such as Cloudflare; the
      signed-in app stays a single-page app. Still one deployable, with no Node server. See
      "Rendering" under Open decisions. The Content-Security-Policy allows the build's inline
      start-up scripts by their hashes, not with `'unsafe-inline'`.

Then the option:

- [x] **Replace `--Identity true|false` with `--Auth none|identity|saas`.** That gives
      2 × 3 × 2 = 12 CI combinations, instead of doubling for each new yes/no option. `saas`
      supports both Marten and EF Core.
- [x] **Organisations (multi-tenancy)** in `saas`: each new account gets its own organisation,
      and a user can create more or be invited into others, with a switcher in the header. The
      access token carries the current organisation (checked against membership when issued;
      switching issues new tokens), and Wolverine reads the tenant ID from that claim and carries
      it through messages. Data is kept apart by Marten's conjoined multi-tenancy or an EF Core
      global query filter, so a forgotten filter can't leak another organisation's data. Todos
      and their realtime updates belong to the organisation.
- [x] **Organisation roles:** Owner, Admin and Member. Owners and admins invite and remove
      members; only owners rename or delete the organisation and change who is an owner; there is
      always at least one owner.
- [x] **Team invitations** by email, to existing users and to new ones (who create an account
      from the invitation).

### v0.4.1: billing and a full back office

Billing, inside `--Auth saas` (no new options):

- [ ] **Stripe subscriptions:** Checkout and the customer portal, with Stripe webhooks turned
      into durable Wolverine messages (the outbox makes webhook handling reliable).
      [Stripe.net](https://github.com/stripe/stripe-dotnet) is open source (Apache-2.0); Stripe
      itself charges fees. Keep payments behind an interface so others can add Paddle or Lemon
      Squeezy, which handle sales tax and VAT.
- [x] **A plan catalogue:** plans, prices per currency, limits and features defined in one place.
      Each plan has a version, so existing customers keep their price when plans change
      ("grandfathering"). The pricing page is generated from it.
- [x] **Free plans and trials:** a free plan is a plan with a price of zero. Trials include a
      "reverse trial": a new organisation starts on the top plan for 14 days, then drops to the
      free plan unless it upgrades.
- [x] **Entitlements and plan limits,** enforced per organisation through one check: "can this
      organisation use feature X?" and "how much of limit Y is left?". Wolverine middleware uses
      it on commands, and the React client uses it to show or hide features and upgrade prompts.
      Built on [Microsoft.FeatureManagement](https://github.com/microsoft/FeatureManagement-Dotnet)
      (MIT), so feature flags and plan features are the same thing.
- [x] **Usage counters** per organisation per billing period, for limits such as "100 items" or
      "50 AI credits a month", with a warning at 80% and a stop at 100%.
- [x] **Downgrades never delete data:** anything over a limit stays but becomes read-only until
      the organisation is back under the limit, and paid features switch off.
- [ ] **A local copy of billing state** (customer, subscription, plan, invoices), kept in step by
      Stripe webhooks, so the app and the back office never call Stripe to answer "which plan is
      this organisation on?".

Admin back office. The admin page manages users only today; the bar for a full back office is
the one in the [template comparison](https://libresharp.github.io/templates/) (PlatformPlatform:
revenue, accounts, users, invoices and feature flags):

- [ ] **Dashboard:** sign-ups, active organisations, monthly recurring revenue, trials ending and
      failed payments.
- [ ] **Organisations:** search; members; plan; usage against limits; invoices; extend a trial or
      give a plan for free; suspend and delete; internal notes.
- [ ] **Users:** as today, plus each user's organisations, ending their sessions, and resending
      the confirmation email.
- [ ] **Subscriptions and invoices:** read-only, with links to the Stripe dashboard. Refunds stay
      in Stripe.
- [ ] **Feature flags:** turn features on globally, per plan or per organisation.
- [ ] **Audit log:** every admin action (who changed what, and when). Needed before "sign in as a
      user" is ever added.
- [ ] **Failed messages:** view and replay Wolverine's dead-letter queue, such as an email that
      failed or a Stripe webhook that errored. JasperFx sells monitoring tooling for this; a small
      free page fits this template's purpose.

### v0.5 onwards: building blocks most SaaS apps need

Added one at a time, in the order that real apps built on the template need them, and taken
from that real use rather than designed in advance. No new options: each goes inside
`--Auth identity` or `saas`, wherever it makes sense.

- [ ] **API keys per organisation:** create, name, revoke and rotate; stored hashed; scopes (read,
      or read and write); rate limits per key.
- [ ] **Outgoing webhooks:** organisations register URLs for events. Each delivery is signed
      (HMAC), sent from a durable Wolverine queue with retries, and logged with its response, so
      owners can see failures and resend.
- [ ] **Incoming webhooks, as a pattern:** check the signature, store the event, hand it to a
      durable message and answer quickly. Billing builds this for Stripe; turn it into a reusable
      helper with a worked example for other providers, such as chat platforms.
- [ ] **File uploads** to S3-compatible storage (MinIO in `docker-compose` for development;
      Cloudflare R2, Backblaze B2 or AWS S3 in production): signed upload URLs, size and type
      limits, image resizing, and storage per organisation counted towards plan limits.
- [ ] **Notifications:** an in-app list delivered through the existing SignalR transport, and
      email preferences with a one-click unsubscribe link.
- [ ] **An MCP endpoint example:** the Todos exposed as MCP tools with the official C# MCP SDK,
      authenticated with an API key and scoped to the organisation, so AI assistants can use the
      app. Check the SDK's licence first.
- [ ] **Encrypted secrets per organisation,** for storing customers' own API keys and OAuth
      tokens, encrypted with ASP.NET Core Data Protection (whose keys the app already keeps in
      PostgreSQL).
- [ ] **Personal data export:** download everything stored about the signed-in user (and, for
      owners, the organisation) as JSON, to go with account deletion. Privacy laws such as GDPR
      and Thailand's PDPA give people this right.

### Extension points for libraries built on the template

Some apps add a shared library on top of a generated app, such as a kit for multi-tenant public
websites. These changes let a library plug in without forking the template:

- [ ] **Swappable tenant resolution:** today the tenant comes from the organisation claim in the
      access token. Put that behind one interface, so an app can also find the tenant from the
      request's host name (for public pages that anonymous visitors see). Wolverine still carries
      one tenant ID through messages either way.
- [ ] **Room for other public pages:** the React Router pre-rendering, the single-page app
      fallback and the 404 handling mustn't assume they own every path outside `/api`.
      Server-rendered pages that an app adds (for example, Razor components) must be able to
      take paths of their own, with their own status codes.
- [ ] **One entitlement interface** (from v0.4.1) that a library can add its own features and
      limits to.
- [ ] **Documented seams:** a page in the generated app's `docs/` on where an app or library adds
      endpoints, messages, admin pages and settings, so its additions survive template updates.

### Later: .NET Aspire for local development (an opt-in option)

- [ ] **`--aspire`**, off by default: adds an Aspire AppHost and ServiceDefaults project, so one
      command starts PostgreSQL, Mailpit, the API and the Vite dev server, with the Aspire
      dashboard showing logs, traces and metrics (Wolverine and Marten emit OpenTelemetry data).
      Aspire is MIT licensed. Docker Compose stays the default for local development, and
      production stays Docker Compose with Caddy, whichever option is chosen. Another yes/no
      option doubles the CI combinations, so consider it after `--Auth` lands, and test
      `--aspire` builds without rerunning every combination's full test suite.

### Frontend quality (any time)

- [x] **UI components:** [shadcn/ui](https://ui.shadcn.com/) (MIT), installed in v0.4.1 so the
      billing and back-office screens are built with it.
- [x] **Every page on shadcn/ui**, with **dark mode** that follows the operating system's
      setting (no script, so pre-rendered pages show the right colours at once). A switch to
      choose light or dark could come later.
- [ ] **Translations** with react-i18next (MIT): the interface's text in resource files, a
      language switcher, and dates and numbers formatted for the user's language. Apps for
      markets outside English-speaking countries need this from the start, so it comes before
      "if users ask".

### Keep out

- Paid tooling and commercial UI kits. Everything stays
  free and open source.

### Open decisions

- [x] Should the `saas` option support both Marten and EF Core, or start Marten-only (simpler
      tenancy, fewer CI combinations)? Both (v0.4.0).
- [x] Should email be on by default whenever Identity is on (Mailpit in development, SMTP
      settings in production)? Yes (v0.3).
- [ ] Which cheap host should the deployment guide target first: a VPS with Docker Compose and
      Caddy, or Fly.io or Railway?
- [x] **Rendering** (decided November 2026): pre-rendering the public pages (above) gives static
      HTML that a CDN serves from cache, which is as fast as a page can be and needs no
      rendering server. It suits pages that are the same for every visitor and change only when
      the app is rebuilt. Public pages that show data changing per request (a public catalogue,
      listings, user-generated content) need rendering per request. For those, the direction is
      server-rendered Razor components (Blazor's static server rendering) **in the same ASP.NET
      Core host**, cached by a CDN, with the React app unchanged for signed-in pages: still one
      deployable, and no Node server. An app or library adds these pages (see "Extension
      points"); they aren't a template option. This replaces the earlier idea of a separate
      Blazor template.

## Later, if users ask

- [ ] **.NET Aspire option:** an app host that starts Postgres, the API and Vite together, in
      place of Docker Compose.
- [ ] **RS256 signing with a JWKS endpoint,** for when a separate service must verify tokens
      without sharing the signing secret. HS256 is enough for this API and its own client.
- [ ] **`sessionStorage` token option,** so logins survive a page reload. Tokens are kept in
      memory today, which is safer against injected scripts.
- [ ] **Running more than one instance:** a SignalR backplane (Redis, MIT-licensed client) and
      rate limits shared between instances.
- [ ] **API types only for the chosen options:** apps without Identity carry the account
      endpoints' types in `schema.d.ts` (harmless, but untidy).

## Upgrades to track

- WolverineFx 7 and Marten 10 once stable (both are release candidates as of November 2026).
- xUnit v3, Alba 9, and the current Node LTS in CI.
- TypeScript 7: the client builds with it, but `npm run gen:api` doesn't, because
  openapi-typescript (7.13.0) needs the TypeScript JavaScript API that TypeScript 7 removed
  (tested November 2026). Dependabot skips TypeScript majors until that changes.

