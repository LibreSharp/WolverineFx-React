# LibreSharp WolverineFx + React Template

A `dotnet new` template for a full-stack app: a [WolverineFx](https://wolverinefx.net/) backend
and a React frontend (Vite, TypeScript, Tailwind CSS), built and deployed as one ASP.NET Core
app on PostgreSQL.

It does the jobs often given to MediatR (in-process messaging) and MassTransit (message bus and
transactional outbox) with Wolverine, using only free, open-source libraries. Part of
[LibreSharp](https://libresharp.github.io), which collects free alternatives to .NET packages that
went commercial. This is a community template, not an official WolverineFx/JasperFx package.

## Get started

You need the .NET 11 SDK, Node.js 22 or later, and Docker with Compose.

```bash
dotnet new install LibreSharp.Templates.WolverineFxReact
dotnet new wolverine-react -n MyApp --Identity true
cd MyApp
docker compose up -d                 # Postgres
dotnet run --project src/MyApp       # the API; leave it running
```

In a second terminal:

```bash
cd MyApp/src/Client
npm install
npm run dev                          # open http://localhost:5173
```

With `--Identity true`, log in as `demo@example.com` / `Demo123!` (created in Development only).

## Options

| Option | Values | Default | |
|---|---|---|---|
| `--Persistence` | `marten`, `efcore` | `marten` | Marten (documents and event sourcing) or EF Core |
| `--Identity` | `true`, `false` | `false` | Login with JWT access tokens and rotating refresh tokens |
| `--IncludeTests` | `true`, `false` | `true` | Integration tests against a real Postgres |

## What you get

- **Wolverine HTTP endpoints** in vertical slices, with FluentValidation returning
  `ProblemDetails`.
- **Messaging with a transactional outbox:** messages are saved with your data and handled in
  the background, surviving restarts.
- **Event sourcing** (Marten): event streams and Wolverine's aggregate handler workflow.
- **Realtime updates** with SignalR: every open tab refreshes, scoped to the signed-in user.
- **Identity** (optional): register, login, refresh-token rotation with reuse detection, and
  per-user data.
- **A typed API client** generated from the OpenAPI document.
- **Production-ready publish:** `dotnet publish` pre-generates Wolverine's code and bundles the
  React app, so production starts fast with no runtime compilation.
- **Tests** with xUnit, [Alba](https://jasperfx.github.io/alba/) and Testcontainers, covering
  endpoints, auth, messaging and SignalR.

Every option combination is built, tested and published in CI on each change.

## Links

- Source, issues and full documentation: https://github.com/LibreSharp/WolverineFx-React
- Roadmap: https://github.com/LibreSharp/WolverineFx-React/blob/main/ROADMAP.md
- LibreSharp: https://libresharp.github.io
