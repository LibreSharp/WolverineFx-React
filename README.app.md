# ApiHost

A full-stack app: a [Wolverine](https://wolverinefx.net/) API on ASP.NET Core and a React frontend
(Vite, TypeScript, Tailwind CSS).
<!--#if (IsMarten) -->
Data lives in PostgreSQL through [Marten](https://martendb.io/).
<!--#else -->
Data lives in PostgreSQL through EF Core.
<!--#endif -->
In production the API also serves the built React app, so it deploys as one service.

## Run it

You need the .NET 11 SDK, Node.js 22.22 or later, and Docker with the Compose plugin.

```bash
docker compose up -d                  # PostgreSQL on localhost:5432
dotnet run --project src/ApiHost        # the API on http://localhost:5133
```

In a second terminal:

```bash
cd src/Client
npm install
npm run dev                           # open http://localhost:5173
```

The Vite dev server forwards `/api` (including the SignalR WebSocket) to the API, so the browser
only talks to one origin.
<!--#if (Identity) -->

Log in as **demo@example.com** / **Demo123!**. This user is created at startup in Development
only, is an admin (so `/admin` works), and the dev server pre-fills the login form with it. Change it under `DevelopmentUser` in
`src/ApiHost/appsettings.Development.json` and in `src/Client/.env.development`.

`docker compose up -d` also starts [Mailpit](https://mailpit.axllent.org/), which catches every
email the app sends in Development (confirmation, password reset): read them at
http://localhost:8025. New accounts must confirm their email before they can log in; the demo
user is already confirmed.
<!--#endif -->

To try the API without the UI, open `src/ApiHost/ApiHost.http` in Visual Studio, Rider, or VS Code
with the REST Client extension, and run the requests in order. Or browse the interactive API
reference at http://localhost:5133/scalar (Development only), generated from the OpenAPI document
at `/openapi/v1.json`.

## Project layout

```
src/ApiHost/
<!--#if (Identity) -->
  Program.cs              All wiring: Wolverine, persistence, validation, SignalR, auth
<!--#else -->
  Program.cs              All wiring: Wolverine, persistence, validation, SignalR
<!--#endif -->
  Features/Todos/         The example feature: endpoints, messages, handler, model
  Realtime/AppHub.cs      SignalR hub that pushes messages to browsers
<!--#if (IsMarten) -->
  Data/Marten/            Marten setup: event store, projections, indexes
<!--#else -->
  Data/EfCore/            AppDbContext, migrations, and migrate-on-startup
<!--#endif -->
<!--#if (Identity) -->
  Identity/               Users, JWT tokens, refresh tokens, auth and account endpoints
  Identity/Email/         Account emails: messages, their handler, and the SMTP sender (MailKit)
  Identity/Admin/         Admin endpoints: users, roles, locks and invitations
  Identity/External/      Sign-in with Google and GitHub
  Identity/Passkeys/      Passkeys: adding them in Settings, and logging in with one
<!--#endif -->
src/Client/src/
  App.tsx                 Routes (React Router)
  components/             Layout (header, navigation, footer), home page, not-found page
  features/todos/         Todo page, TanStack Query hooks, SignalR hook (useTodoRealtime)
<!--#if (Identity) -->
  features/auth/          Login, register, email confirmation and password reset pages, route guards
  features/account/       Settings page and the header's user menu
  features/admin/         Admin page (/admin): users, roles, locks and invitations
<!--#endif -->
  lib/api/                Typed API client generated from the OpenAPI document
  test/                   Unit test setup: the fake API (MSW) and render helpers
<!--#if (IncludeTests) -->
tests/ApiHost.Tests/        Integration tests against a real PostgreSQL
tests/e2e/                  End-to-end tests in a real browser (Playwright)
<!--#endif -->
```

## How the example works

The Todo feature is a working example of each pattern. Copy it when you add your own features.

- **Endpoints.** Each endpoint is a static method with `[WolverineGet]` or `[WolverinePost]`.
  Wolverine finds it automatically and injects what it asks for. See `Features/Todos/`.
- **Validation.** `CreateTodoValidator` (FluentValidation) runs before the endpoint. An invalid
  request gets a 400 response with `ProblemDetails`, which the React form displays.
- **Messages and the outbox.** Completing a Todo returns a `TodoCompleted` message from the
  endpoint. Wolverine saves it in the same database transaction as the change (the transactional
  outbox) and `TodoCompletedHandler` handles it in the background, even after a restart.
<!--#if (IsMarten) -->
- **Event sourcing.** A Todo is a stream of events (`TodoCreated`, then `TodoCompleted`). The
  complete endpoint uses Wolverine's aggregate handler workflow: `[WriteAggregate]` loads the
  Todo from its events, and the method returns the events to append. An inline snapshot
  projection keeps a `Todo` document up to date for queries.
<!--#endif -->
- **Scheduled messages.** "Remind me…" on a Todo calls `POST /api/todos/{id}/remind`, which
  schedules a `TodoReminderDue` message for later (`Features/Todos/RemindTodo.cs`). Scheduled
  messages are stored in PostgreSQL, so reminders still fire after a restart. When it's due,
  `TodoReminderDueHandler` pushes a `todo-reminder` to the browser, unless the Todo was completed
  in the meantime.
- **Realtime updates.** Creating or completing a Todo sends a `todo-changed` message to the
  browser over SignalR (`TodoChanged`, routed in `Program.cs`), and every open tab refetches.
<!--#if (Identity) -->
  Each user only receives updates about their own Todos.
<!--#endif -->
- **Frontend data.** TanStack Query caches the Todo list (`features/todos/todoQueries.ts`).
  Changes and SignalR messages invalidate it, which refetches it.
<!--#if (Identity) -->
- **Authentication.** Login returns a short-lived JWT access token and a refresh token. The
  client refreshes automatically when a request gets a 401, each refresh replaces the refresh
  token, and reusing an old one revokes the user's tokens. Tokens are kept in memory, not in
  browser storage, so reloading the page logs you out. Each user sees only their own Todos.
- **Account emails.** Registering emails a link to `/confirm-email`, and login is refused (403,
  with a "send it again" button) until it's opened. Changing the email in Settings emails a link
  to the new address, and the email only changes when it's opened. Endpoints don't send email
  themselves: they return a message (`SendEmailConfirmation`, `SendPasswordReset`, ...) that
  Wolverine stores in its durable inbox, and `AccountEmailHandler` sends it in the background,
  retrying if the mail server is down, so no email is lost on a restart. The handler creates the
  one-time token when it sends, so tokens are never stored in the message tables.
- **Account settings** (`/settings`). Users can change their email (after confirming the new
  one) and display name, change their password (which signs out their other sessions), and
  delete their account (password required). Deleting publishes an `AccountDeleted` message; each feature that stores user data
  handles it to delete that data (see `Features/Todos/DeleteTodosOfDeletedAccountHandler.cs`).
- **Forgot password.** `/forgot-password` emails a link to `/reset-password` with a one-time
  token valid for an hour. The API answers the same whether or not the email has an account, so
  it can't be used to find out who is registered. Resetting also confirms the email.
- **Account lockout.** Repeated wrong passwords lock the account for a while (ASP.NET Core
  Identity's lockout, configured in `Identity/IdentityJwtSetup.cs`). While locked, login answers
  403 without checking the password, refresh tokens stop working, and resetting the password
  lifts the lock.
- **Roles and the admin page** (`/admin`, Admin role only). Roles are constants in
  `Identity/AppRoles.cs`, created in the database at startup (`RoleSeeder`); protect endpoints
  with `[Authorize(Roles = AppRoles.Admin)]`. Admins list and search users, change their roles,
  lock and unlock accounts (an admin lock has no end and ends the user's sessions), and invite
  people: an invitation creates the account and emails a link to choose a password. Accounts
  whose email is in `Accounts:AdminEmails` become admins once their email is confirmed. Set
  `Accounts:AllowRegistration` to `false` to make accounts invitation-only.
- **Sign-in with Google and GitHub** (`Identity/External/`), each switched on by setting its
  client ID and secret. The provider must vouch for the email address: a verified email signs
  into (and links to) the account with that email, or creates one. GitHub's primary email is
  used only if GitHub has verified it. If an account with that email was registered but never
  confirmed, its password is removed first, so whoever registered it can't get in. After the
  provider, the API redirects to `/external-login` with a refresh token in the URL fragment
  (never sent to servers), which the client swaps for new tokens at once. Settings lists, links
  and removes providers; the last way to sign in can't be removed.

- **Passkeys.** Users add passkeys in Settings and can then log in with one instead of a
  password ("Log in with a passkey"; the browser offers the passkeys it has for the site, so no
  email is typed). This is ASP.NET Core Identity's own WebAuthn support (`IPasskeyHandler`), used
  without cookies: the ceremony's state is encrypted and handed to the client for a few minutes
  (`Identity/Passkeys/PasskeySetup.cs`). The relying party ID is the site's host name, so
  passkeys created on one domain don't work on another. Settings won't remove the last way a
  user can sign in (password, linked provider or passkey).

  To set one up, create an OAuth app with the provider and register its callback URL:
  `https://your-domain/api/auth/external/signin-google` (or `signin-github`); in Development,
  `http://localhost:5133/api/auth/external/signin-google`. Then set the settings below; in
  Development keep them out of source control with user secrets: in `src/ApiHost`, run
  `dotnet user-secrets init` once, then `dotnet user-secrets set Authentication:Google:ClientId ...`.
<!--#endif -->

## Adding a feature

1. Create `src/ApiHost/Features/<Name>/` with your endpoint classes, request records, and any
   messages and handlers. No registration is needed: Wolverine finds endpoints by their
   attributes, and handlers by name (a class ending in `Handler` with a `Handle` method).
2. Add a validator (`AbstractValidator<TRequest>`) for any request that needs one.
<!--#if (IsMarten) -->
3. Store documents with `IDocumentSession` (Marten saves them for you after the endpoint runs),
   or follow the Todo example for an event-sourced aggregate.
<!--#else -->
3. Add a `DbSet` to `Data/EfCore/AppDbContext.cs` and create a migration (see below). Wolverine
   saves the `AppDbContext` for you after the endpoint runs.
<!--#endif -->
4. To push a message to browsers, add `opts.PublishMessage<YourMessage>().ToSignalR();` in
   `Program.cs`.
<!--#if (Identity) -->
5. If the feature stores data about a user, add a handler for `AccountDeleted` that deletes it,
   like `Features/Todos/DeleteTodosOfDeletedAccountHandler.cs`.
6. With the API running, update the client's types with `npm run gen:api` (in `src/Client`), then
   add a folder under `src/Client/src/features/` and a route in `App.tsx`.
<!--#else -->
5. With the API running, update the client's types with `npm run gen:api` (in `src/Client`), then
   add a folder under `src/Client/src/features/` and a route in `App.tsx`.
<!--#endif -->

## Observability

The app records traces, metrics and logs with OpenTelemetry: HTTP requests, SQL commands,
<!--#if (IsMarten) -->
Wolverine messages and handlers, Marten, and .NET runtime metrics
<!--#else -->
Wolverine messages and handlers, and .NET runtime metrics
<!--#endif -->
(`src/ApiHost/Observability/OpenTelemetrySetup.cs`). Nothing is sent anywhere until you set
`OTEL_EXPORTER_OTLP_ENDPOINT`. To see it locally in the Aspire dashboard:

```bash
docker compose --profile observability up -d     # dashboard on http://localhost:18888
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project src/ApiHost
```

(`dotnet run` itself also reports to the dashboard, as `dotnet-cli`; the app is `ApiHost`.) In
production, point `OTEL_EXPORTER_OTLP_ENDPOINT` (and, if needed, `OTEL_EXPORTER_OTLP_HEADERS`) at
any OpenTelemetry backend, such as Grafana, Honeycomb or a self-hosted Jaeger.

## Common tasks

| Task | Command |
|---|---|
<!--#if (IncludeTests) -->
| Run the tests (Docker must be running) | `dotnet test` |
<!--#endif -->
| Run the client's unit tests | `npm test` in `src/Client` (or `npm run test:watch`) |
<!--#if (IncludeTests) -->
| Run the end-to-end tests (the app running; see below) | `npm install` once, then `npm test` in `tests/e2e` |
<!--#endif -->
| Update the client's API types | `npm run gen:api` in `src/Client`, with the API running |
| Lint the client | `npm run lint` in `src/Client` |
<!--#if (IsEfCore) -->
| Add a database migration | `dotnet ef migrations add <Name> --project src/ApiHost --output-dir Data/EfCore/Migrations` |
<!--#endif -->
| List the app's command-line tools (code generation, database, diagnostics) | `dotnet run --project src/ApiHost -- help` |
| Publish for production | `dotnet publish src/ApiHost -c Release -o publish` |
<!--#if (IsEfCore) -->

`dotnet ef` is a separate tool: install it once with `dotnet tool install --global dotnet-ef`.
Migrations are applied automatically when the app starts.
<!--#else -->

Marten creates and updates its tables automatically when the app starts.
<!--#endif -->
<!--#if (IncludeTests) -->

The API tests start their own throwaway PostgreSQL in Docker (Testcontainers) and call the API
over HTTP with [Alba](https://jasperfx.github.io/alba/). They cover:

- the Todo endpoints and validation, health checks, security headers and the API reference,
- the background `TodoCompleted` handler and scheduled reminders (with Wolverine's message
  tracking),
- SignalR notifications, received by real SignalR clients,
<!--#if (IsMarten) -->
- the Todo event stream,
<!--#endif -->
<!--#if (Identity) -->
- registration and email confirmation (following the links in captured emails), login, token
  refresh and reuse detection, logout and per-user access,
- account settings, email changes, password reset, account deletion, lockout and rate limiting,
- roles, the admin endpoints and invitations, Google/GitHub sign-in rules, and passkeys (with a
  software authenticator, so ASP.NET Core Identity's real WebAuthn checks run),
- every endpoint's security attributes (`EndpointSecurityTests`): a new account or admin
  endpoint that forgets `[Authorize]`, the Admin role or rate limiting fails it.
<!--#endif -->
<!--#endif -->

The client's unit tests (`npm test` in `src/Client`) use Vitest and React Testing Library, with
the API faked by [MSW](https://mswjs.io/). They sit next to the code they test (`*.test.tsx`) and
<!--#if (Identity) -->
cover the Todo page, the API client's token refresh, the login, registration, email-link,
invitation and external sign-in pages, route guards, Settings and the admin page.
<!--#else -->
cover the Todo page and the API client.
<!--#endif -->
<!--#if (IncludeTests) -->

The end-to-end tests (`tests/e2e`) drive the running app in Chrome with
[Playwright](https://playwright.dev/), using the Chrome installed on your machine.
<!--#if (Identity) -->
They sign up and confirm accounts with the real emails in Mailpit, use Todos with live updates
across tabs, change settings, reset a password, trip the lockout, use the admin page and
invitations, and log in with a passkey (Chrome's virtual authenticator). To run them against
the Development setup, start the API with a higher sign-in rate limit (every test comes from
your machine) and the Vite dev server, then in `tests/e2e`:

```bash
Accounts__AuthAttemptsPerMinute=1000 dotnet run --project src/ApiHost   # in one terminal
npm run dev                                                              # in src/Client
E2E_BASE_URL=http://localhost:5173 E2E_ADMIN_EMAIL=demo@example.com E2E_ADMIN_PASSWORD='Demo123!' npm test
```
<!--#else -->
They check the home page and that Todos are added, completed and shown live in other tabs. Run
the API and the Vite dev server, then in `tests/e2e`: `E2E_BASE_URL=http://localhost:5173 npm test`.
<!--#endif -->
<!--#endif -->

## Deploying

`deploy/` has everything to run the app on a small server: a production `docker-compose.yml`
(PostgreSQL, the app's container, and Caddy for automatic HTTPS) and a step-by-step guide in
`deploy/README.md`. The container image is built with
`dotnet publish src/ApiHost -c Release /t:PublishContainer`.

The rest of this section applies wherever you run it.

`dotnet publish` builds the React app into `wwwroot` and pre-generates Wolverine's handler code,
so production starts quickly and compiles nothing at runtime. (In Development, Wolverine generates
that code at runtime instead, so you never need to regenerate it by hand.)

Run the published app from its own folder, so it finds `wwwroot`:

```bash
cd publish
ASPNETCORE_ENVIRONMENT=Production dotnet ApiHost.dll
```

For your host's health checks, use `/health/live` (the process is running) and `/health/ready`
(it can also reach PostgreSQL).

Configure it with environment variables (or any ASP.NET Core configuration source):

| Setting | Example |
|---|---|
<!--#if (IsMarten) -->
| `ConnectionStrings__Marten` | `Host=db;Database=apihost;Username=app;Password=…` |
<!--#else -->
| `ConnectionStrings__Postgres` | `Host=db;Database=apihost;Username=app;Password=…` |
<!--#endif -->
| `ASPNETCORE_URLS` | `http://+:8080` |
<!--#if (Identity) -->
| `Jwt__SigningKey` | A random secret of at least 32 bytes |
| `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__UserName`, `Email__Smtp__Password`, `Email__Smtp__From` | Your email service's SMTP settings, for account emails |
| `Accounts__AdminEmails__0` | Your email: that account becomes an admin once its email is confirmed (more: `__1`, `__2`, ...) |
| `Accounts__AllowRegistration` | `false` to allow only invited people to create accounts (default `true`) |
| `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret` | Optional: turns on "Continue with Google" |
| `Authentication__GitHub__ClientId`, `Authentication__GitHub__ClientSecret` | Optional: turns on "Continue with GitHub" |
| `Accounts__AuthAttemptsPerMinute` | Sign-in and account attempts allowed per client address per minute (see Security) |
| `Email__Smtp__Security` | `Auto` (default: TLS when the server offers it), `StartTls`, `SslOnConnect` (port 465) or `None` |

The app refuses to start outside Development if `Jwt:SigningKey` is missing, too short, or still
the development key. The demo user is never created outside Development. Without
`Email:Smtp:Host`, no emails are sent and the app logs a warning instead (never the link). Since
new accounts must confirm their email, configure SMTP before people register. Emails that still
fail after Wolverine's retries end up in the `wolverine_dead_letters` table.

ASP.NET Core Data Protection keys, which protect the tokens in account emails, are stored in PostgreSQL,
so reset links keep working across restarts and on every instance of the app. They're stored
unencrypted, like the rest of your data: protect database access and backups accordingly.
<!--#endif -->

## Security

What the app does to protect its users and data, and what's left to you when you deploy it.

### Built in

- **Browser security headers** on every response: a Content-Security-Policy that only allows
  scripts, styles and connections from the app's own origin, plus protections against MIME
  sniffing, framing (clickjacking) and leaking URLs in the Referer header
  (`src/ApiHost/Security/SecurityHeaders.cs`). If you add a third-party script, font or analytics
  service, add its origin there.
- **HTTPS only in production:** requests are redirected to HTTPS and browsers are told to keep
  using it (HSTS).
- **Development-only tools stay out of production:** the OpenAPI document and the API reference
  page (`/scalar`) are only served in Development, and the React app's scripts are served from the
  app itself, not a CDN.
- **No runtime code compilation in production:** Wolverine's code is generated at publish time
  and the app fails at startup if any of it is missing.
<!--#if (Identity) -->

### Accounts and sign-in

- **Passwords** are hashed by ASP.NET Core Identity and must meet its strength rules; they're
  never logged or returned.
- **Short-lived access tokens, rotating refresh tokens.** Access tokens (signed JWTs) expire after
  a few minutes. Refresh tokens are stored only as hashes, replaced on every use, and reusing an
  old one (a sign it was stolen) signs the user out everywhere. The browser keeps tokens in memory,
  never in `localStorage`, so injected scripts can't read them from storage.
- **The signing key is checked at startup:** outside Development the app refuses to start with a
  missing, too-short or development key.
- **Email ownership is verified.** New accounts must confirm their email before they can log in,
  and a changed email only takes effect once the new address is confirmed.
- **One-time, short-lived links.** Confirmation, password reset and invitation links carry tokens
  that expire, stop working once used, and are created only when the email is sent, so they're
  never stored in the database's message tables or written to logs.
- **No account enumeration where it matters:** "forgot password" and "resend confirmation" answer
  the same whether or not an account exists, and a login only says an email is unconfirmed to
  someone who knows the password.
- **Account lockout** (ASP.NET Core Identity's): repeated wrong passwords lock the account for a
  while, whichever addresses they come from. A locked account can't refresh its tokens. A password
  reset lifts a lockout from failed logins. Tune it in `Identity/IdentityJwtSetup.cs`.
- **Rate limiting:** endpoints that check passwords, send email or create accounts allow a small
  number of attempts per client address per minute (`Identity/AuthRateLimiting.cs`).
- **Sessions end when they should:** changing or resetting a password, deleting the account, and
  an admin lock all revoke the user's refresh tokens.
- **Each user sees only their own data:** another user's records answer 404, so their existence
  isn't revealed. Deleting an account (which needs the password again) deletes the user's data
  too, including Todo event history.

### Roles and the admin page

- Roles are checked by the API on every admin request; hiding the admin page in the browser is
  only for convenience.
- Admins can't lock their own account or remove their own Admin role, so an admin always remains.
- `Accounts:AdminEmails` only grants Admin once that email has been confirmed, so nobody can claim
  it by registering the address first.
- Registration can be made invitation-only (`Accounts:AllowRegistration=false`).

### Google, GitHub and passkeys

- **External sign-in** only trusts email addresses the provider has verified. If someone had
  registered that email without confirming it, their password is removed before the account is
  linked, so they can't get in afterwards (account pre-hijacking). Linking a provider from Settings
  uses a short-lived encrypted ticket, and the tokens handed back to the browser are passed in the
  URL fragment (never sent to a server) and exchanged immediately.
- **Passkeys** use ASP.NET Core Identity's WebAuthn support, which checks the challenge, the site's
  origin, the signature and the passkey's counter. The ceremony state the browser holds is
  encrypted, expires after a few minutes, and is tied to the user when adding a passkey.
- Settings won't remove a user's last way to sign in.
<!--#endif -->

### When you deploy

- Keep secrets out of source control: use environment variables, `deploy/.env` (on the server
  only) or a secret store.
- Expose only the reverse proxy. In `deploy/docker-compose.yml` only Caddy is reachable; the app
  and database aren't.
<!--#if (Identity) -->
- Set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` behind a reverse proxy (the production compose
  file does), so rate limits apply to each client's real address and HTTPS is detected.
- Use a long random `Jwt:SigningKey`, configure SMTP before people register, and set
  `Accounts:AdminEmails`.
- ASP.NET Core Data Protection keys (which protect the tokens in emails, link tickets and passkey
  state) are stored in PostgreSQL so they work across restarts and instances. Like the rest of
  your data they're unencrypted at rest: protect database access and backups.
<!--#endif -->
- Keep dependencies current: `.github/dependabot.yml` opens weekly update pull requests.

## Created from a template

This project was created from the
[LibreSharp WolverineFx + React template](https://github.com/LibreSharp/WolverineFx-React)
(`dotnet new wolverine-react`). See that repository for its documentation, roadmap and issues.
Newer template versions (`dotnet new update`) affect new projects only, not this one. You can
delete this section.
