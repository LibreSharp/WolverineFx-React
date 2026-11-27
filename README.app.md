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
email the app sends in Development (confirmation, password reset, invitations): read them at
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
<!--#if (Identity) -->
  Features/Auth/          Log in (password, Google/GitHub, passkey), register, confirm email, reset password
  Features/Account/       The signed-in user: profile, password, sign-in methods, passkeys, delete account
  Features/Admin/         Admin endpoints: users, roles, locks and invitations
<!--#endif -->
<!--#if (Saas) -->
  Features/Organisations/ Organisations: switching, members and roles, invitations, joining
  Organisations/          The organisation model, its store, and choosing a session's organisation
<!--#endif -->
  Realtime/AppHub.cs      SignalR hub that pushes messages to browsers
<!--#if (IsMarten) -->
  Data/Marten/            Marten setup: event store, projections, indexes
<!--#else -->
  Data/EfCore/            AppDbContext, migrations, and migrate-on-startup
<!--#endif -->
<!--#if (Identity) -->
  Identity/               Users, JWT and refresh tokens, roles, lockout, rate limits (used by the features)
  Identity/Email/         Account emails: messages, their handler, and the SMTP sender (MailKit)
  Identity/External/      Google and GitHub set-up, and linking a provider's account to a user
  Identity/Passkeys/      Passkey set-up (Identity's WebAuthn support)
<!--#endif -->
src/Client/src/
  root.tsx, App.tsx       The HTML document (with the description and link-preview tags), and the page frame
  routes.ts, routes/      Each URL and the route module for its page, with the page's title
  pages.ts                Every page's path, and which are public: pre-rendered, and in sitemap.xml
  components/             Layout (header, navigation, footer), home page, not-found page
  features/todos/         Todo page, TanStack Query hooks, SignalR hook (useTodoRealtime)
<!--#if (Identity) -->
  features/auth/          Login, register, email confirmation and password reset pages, route guards
  features/account/       Settings page and the header's user menu
  features/admin/         Admin page (/admin): users, roles, locks and invitations
<!--#endif -->
<!--#if (Saas) -->
  features/organisations/ The organisation switcher, /organisation (members, invitations) and /join
<!--#endif -->
  lib/api/                Typed API client generated from the OpenAPI document
  test/                   Unit test setup: the fake API (MSW) and render helpers
docs/                       Documentation and how-to guides
deploy/                     Production docker-compose, Caddy, and a server guide
<!--#if (IncludeTests) -->
tests/ApiHost.Tests/        Integration tests against a real PostgreSQL
tests/e2e/                  End-to-end tests in a real browser (Playwright)
<!--#endif -->
```

## Documentation

The [docs](docs/README.md) folder explains how the app works and how to change it:

- [Architecture](docs/architecture.md): the patterns the Todo example shows, and how to copy them.
<!--#if (Identity) -->
- [Accounts](docs/accounts.md): sign-in, emails, settings, lockout, roles and the admin page,
  Google/GitHub and passkeys.
<!--#if (Saas) -->
  Organisations, their members and invitations are there too.
<!--#endif -->
<!--#endif -->
- [Security](docs/security.md): every protection the app has, and a checklist for deploying.
- [Testing](docs/testing.md), [Observability](docs/observability.md) and
  [Deployment](docs/deployment.md) (with all the production settings).
- How-to guides:
  - [Add a feature](docs/how-to/add-a-feature.md)
  - [Put Cloudflare in front](docs/how-to/put-cloudflare-in-front.md)
<!--#if (IsEfCore) -->
  - [Change the database schema](docs/how-to/add-a-migration.md)
<!--#endif -->
<!--#if (Identity) -->
  - [Send email through your email provider](docs/how-to/configure-email.md)
  - [Set up Google and GitHub sign-in](docs/how-to/set-up-external-sign-in.md)
  - [Add a role](docs/how-to/add-a-role.md) and [make someone an admin](docs/how-to/make-an-admin.md)
  - [Change the JWT signing key](docs/how-to/rotate-the-signing-key.md)
<!--#endif -->

## Common tasks

| Task | Command |
|---|---|
<!--#if (IncludeTests) -->
| Run the API tests (Docker must be running) | `dotnet test` |
<!--#endif -->
| Run the client's unit tests | `npm test` in `src/Client` |
<!--#if (IncludeTests) -->
| Run the end-to-end tests ([how](docs/testing.md#end-to-end-tests)) | `npm test` in `tests/e2e`, with the app running |
<!--#endif -->
| Update the client's API types | `npm run gen:api` in `src/Client`, with the API running |
| Lint the client | `npm run lint` in `src/Client` |
<!--#if (IsEfCore) -->
| Add a database migration ([how](docs/how-to/add-a-migration.md)) | `dotnet ef migrations add <Name> --project src/ApiHost --output-dir Data/EfCore/Migrations` |
<!--#endif -->
| List the app's command-line tools (code generation, database, diagnostics) | `dotnet run --project src/ApiHost -- help` |
| Publish for production ([deployment](docs/deployment.md)) | `dotnet publish src/ApiHost -c Release -o publish` |

## Created from a template

This project was created from the
[LibreSharp WolverineFx + React template](https://github.com/LibreSharp/WolverineFx-React)
(`dotnet new wolverine-react`). See that repository for its documentation, roadmap and issues.
Newer template versions (`dotnet new update`) affect new projects only, not this one. You can
delete this section.
