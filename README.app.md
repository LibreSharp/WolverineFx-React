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
only, and the dev server pre-fills the login form with it. Change it under `DevelopmentUser` in
`src/ApiHost/appsettings.Development.json` and in `src/Client/.env.development`.

In Development, emails aren't sent: "Forgot your password?" writes the reset link to the API's
console output, where you can open it.
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
  Identity/Email/         Account emails (password reset): SMTP, or the log in Development
<!--#endif -->
src/Client/src/
  App.tsx                 Routes (React Router)
  components/             Layout (header, navigation, footer), home page, not-found page
  features/todos/         Todo page, TanStack Query hooks, SignalR hook (useTodoRealtime)
<!--#if (Identity) -->
  features/auth/          Login, register and password reset pages, route guards
  features/account/       Settings page and the header's user menu
<!--#endif -->
  lib/api/                Typed API client generated from the OpenAPI document
<!--#if (IncludeTests) -->
tests/ApiHost.Tests/        Integration tests against a real PostgreSQL
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
- **Account settings** (`/settings`). Users can change their email and display name, change
  their password (which signs out their other sessions), and delete their account (password
  required). Deleting publishes an `AccountDeleted` message; each feature that stores user data
  handles it to delete that data (see `Features/Todos/DeleteTodosOfDeletedAccountHandler.cs`).
- **Forgot password.** `/forgot-password` emails a link to `/reset-password` with a one-time
  token valid for an hour. The API answers the same whether or not the email has an account, so
  it can't be used to find out who is registered.
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

The tests start their own throwaway PostgreSQL in Docker (Testcontainers) and call the API over
HTTP with [Alba](https://jasperfx.github.io/alba/). They cover:

- the Todo endpoints and validation,
- the background `TodoCompleted` handler (with Wolverine's message tracking),
- SignalR notifications, received by real SignalR clients,
<!--#if (IsMarten) -->
- the Todo event stream,
<!--#endif -->
<!--#if (Identity) -->
- registration, login, token refresh and reuse detection, logout, and per-user access.
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
| `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__UserName`, `Email__Smtp__Password`, `Email__Smtp__From` | Your email service's SMTP settings, for password reset emails |

The app refuses to start outside Development if `Jwt:SigningKey` is missing, too short, or still
the development key. The demo user is never created outside Development. Without
`Email:Smtp:Host`, password reset emails can't be sent; the app logs a warning instead (never the
link).

ASP.NET Core Data Protection keys, which protect password reset tokens, are stored in PostgreSQL,
so reset links keep working across restarts and on every instance of the app. They're stored
unencrypted, like the rest of your data: protect database access and backups accordingly.
<!--#endif -->

## Security

- Every response carries browser security headers, including a Content-Security-Policy that only
  allows scripts, styles and connections from the app's own origin
  (`src/ApiHost/Security/SecurityHeaders.cs`). If you add a third-party script, font or analytics
  service, add its origin there. Production also sends HSTS.
<!--#if (Identity) -->
- Login, registration, password reset and change, and account deletion allow 10 attempts a minute
  per client IP, then answer 429 (`src/ApiHost/Identity/AuthRateLimiting.cs`). Behind a reverse
  proxy, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the app sees each client's real
  address, and only expose the app through the proxy.
<!--#endif -->

## Created from a template

This project was created from the
[LibreSharp WolverineFx + React template](https://github.com/LibreSharp/WolverineFx-React)
(`dotnet new wolverine-react`). See that repository for its documentation, roadmap and issues.
Newer template versions (`dotnet new update`) affect new projects only, not this one. You can
delete this section.
