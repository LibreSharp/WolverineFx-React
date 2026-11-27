# AGENTS.md

Guidance for AI coding assistants working on ApiHost. See README.md for the overview and `docs/`
for how each part works (architecture, accounts, security, testing, deployment) and how-to guides
for common changes.

## Stack

<!--#if (IsMarten) -->
- API: ASP.NET Core (.NET 11) with Wolverine (HTTP endpoints and messaging), PostgreSQL through
  Marten (documents and event sourcing).
<!--#else -->
- API: ASP.NET Core (.NET 11) with Wolverine (HTTP endpoints and messaging), PostgreSQL through
  EF Core.
<!--#endif -->
- Client: React, TypeScript, Vite, Tailwind CSS, shadcn/ui, React Router, TanStack Query, SignalR.
<!--#if (Identity) -->
- Auth: ASP.NET Core Identity with JWT access tokens and rotating refresh tokens.
<!--#endif -->

## Commands

| Task | Command |
|---|---|
| Build | `dotnet build` |
<!--#if (IncludeTests) -->
| Test (Docker must be running) | `dotnet test` |
<!--#endif -->
| Run the API | `docker compose up -d` then `dotnet run --project src/ApiHost` |
| Run the client | `npm run dev` in `src/Client` |
| Test the client | `npm test` in `src/Client` (`npm run test:watch` while working) |
<!--#if (IncludeTests) -->
| End-to-end tests (the app running) | `npm test` in `tests/e2e` (see README.md) |
<!--#endif -->
| Lint and type-check the client | `npm run lint` and `npm run build` in `src/Client` |
| Regenerate the client's API types | `npm run gen:api` in `src/Client`, with the API running |
<!--#if (IsEfCore) -->
| Add a migration | `dotnet ef migrations add <Name> --project src/ApiHost --output-dir Data/EfCore/Migrations` |
<!--#endif -->

Build with 0 warnings: Meziantou.Analyzer runs on every project; fix its findings rather than
suppressing them, unless `.editorconfig` explains why a rule is off.

## Backend conventions

- **Vertical slices.** A feature lives in `src/ApiHost/Features/<Name>/`: request records, their
  FluentValidation validators, endpoints, messages and handlers, often in one file.
- **Endpoints** are static methods with `[WolverineGet]`, `[WolverinePost]` and so on. Wolverine
  finds them; don't register them. Inject what the method needs as parameters.
- **Handlers** are found by name: the class name must end in `Handler` and the method is `Handle`.
  A class named anything else is silently ignored.
- **Don't call `SaveChanges`.** `AutoApplyTransactions` saves the Marten session or EF Core
  `DbContext` after an endpoint or handler, together with its outgoing messages (the outbox).
- **Send messages by returning them** (`OutgoingMessages`, or a returned message), not by calling
  the bus, so they're stored by the outbox in the same transaction.
- **Validation:** add an `AbstractValidator<TRequest>`; failures become 400 `ProblemDetails`.
- **Browser updates:** give the message `[MessageIdentity("kebab-name")]` and route it with
  `opts.PublishMessage<T>().ToSignalR()` in `Program.cs`; the client matches on that name.
<!--#if (IsMarten) -->
- **Event sourcing:** follow `Features/Todos/CompleteTodoEndpoint.cs` (`[WriteAggregate]`, return the
  events to append) and register a projection in `Data/Marten/MartenPersistence.cs`.
<!--#else -->
- **Data:** add a `DbSet` to `Data/EfCore/AppDbContext.cs` and a migration. Never edit existing
  migrations by hand.
<!--#endif -->
<!--#if (Identity) -->
<!--#if (Saas) -->
- **Organisations (tenants):** data that belongs to an organisation implements `ITenanted`
  (like `Features/Todos/Todo.cs`).
<!--#if (IsMarten) -->
  Register it with `.MultiTenanted()` in `Data/Marten/MartenPersistence.cs`.
<!--#endif -->
  Wolverine then filters it to the request's organisation; don't filter by organisation by hand. Handle `OrganisationDeleted` to delete it,
  like `Features/Todos/DeleteTodosOfDeletedOrganisationHandler.cs`. Check organisation roles with
  `IOrganisationStore` and the rules in `Features/Organisations/OrganisationRules.cs`.
  `IOrganisationStore` saves each change at once, outside the endpoint's transaction; a change
  that must send a message with it publishes through Wolverine's outbox in the store, as
  `DeleteAsync` and `AddInvitationAsync` do.
- **Plans:** check what an organisation's plan allows through `Entitlements` (`Billing/`), never
  by the plan's name: `RequireFeatureAsync`/`RequireRoomAsync` in a `Before` method, `TryUseAsync`
  for PerPeriod limits, and `IsEnabledAsync` in handlers that run later. Plans, features and
  limits are defined only in `Billing/Plans.cs`; change a plan customers are on by adding a
  version. A downgrade never deletes data. Billing state comes from the local `BillingAccount`,
  kept in step by the payment provider's webhooks: never call Stripe to decide what a plan
  allows. See `docs/billing.md`.
- **Integrations:** a program's API key acts as its creator in its organisation and only reaches
  `ApiKeys:AllowedPaths`: add a new API's path there if keys should use it. When something
  happens that customers may want to hear about, add a `WebhookEvents` constant and return
  `WebhookEvents.Raise(...)` with the endpoint's or handler's messages. Store customers' keys and
  tokens with `OrganisationSecrets`, never in plain columns. New organisation data: register an
  `IOrganisationDataSource` (its owners' download) and delete it in an `OrganisationDeleted`
  handler. See `docs/integrations.md`.
<!--#endif -->
- **Auth:** protect endpoints with `[Authorize]` (or `[Authorize(Roles = AppRoles.Admin)]`; roles
  are constants in `Identity/AppRoles.cs`) and scope a user's own data to `user.GetUserId()`.
  Answer 404, not 403, for data the user can't see. Add `[EnableRateLimiting(AuthRateLimiting.Policy)]` to
  endpoints that check passwords or create accounts.
<!--#if (Saas) -->
- **User data:** a feature that stores data about a user (not their organisation) must handle
  `AccountDeleted` and delete it, and register an `IPersonalDataSource` for "Download your data".
<!--#else -->
- **User data:** a feature that stores data about a user must handle `AccountDeleted` and delete
  it, like `Features/Todos/DeleteTodosOfDeletedAccountHandler.cs`, and register an
  `IPersonalDataSource` for "Download your data", like `Features/Todos/TodosPersonalData.cs`.
<!--#endif -->
- **Notifications:** tell a user something by publishing `SendNotification` (with a category from
  `NotificationCategories`); it's listed, pushed live and emailed unless they turned it off.
- **Email:** never call `IEmailSender` from an endpoint. Return a message and send it from a
  handler, as `Identity/Email/AccountEmails.cs` does, so it goes through the durable outbox and
  is retried. Create one-time tokens in the handler, not the endpoint, so they aren't stored.
  Build links with `ClientUrl.For` (`App:ClientUrl`), never from the request's `Host` header,
  which the caller controls. Endpoints take tokens in request bodies, not URLs, which are logged
  and traced.
- **Identity:** use `UserManager`/`SignInManager` for account rules (password checks, lockout,
  tokens) rather than reimplementing them.
<!--#endif -->
- **Packages:** versions live in `Directory.Packages.props` (central package management); project
  files reference packages by name only. Keep everything free and open source.
- **Security headers:** a new external script, font or API origin must be added to the
  Content-Security-Policy in `Security/SecurityHeaders.cs`.

## Client conventions

- Features live in `src/Client/src/features/<name>/`. Pages are React Router routes (framework
  mode, single-page app): a URL in `src/Client/src/routes.ts`, pointing to a route module in
  `src/Client/src/routes/` that default-exports the page and has a `handle` with its title. Add
  the path to `src/Client/src/pages.ts` too (a unit test checks they match); `index: true` there
  and in the handle makes a page public: indexed by search engines and pre-rendered to HTML.
  Pre-rendered pages run in Node at build time, so they must not touch `window` while rendering.
- UI: build with the shadcn/ui components in `src/Client/src/components/ui/` (Button, Card,
  Input, NativeSelect, Table, Dialog...), the app's `components/Section.tsx` and
  `components/Messages.tsx`, and the theme's colours (`bg-card`, `text-muted-foreground`,
  `text-destructive`), not fixed ones (`bg-white`, `text-gray-600`), so pages work in dark mode
  (which follows the operating system). Add a component with
  `npx shadcn@latest add <name>` in `src/Client`; the files are the app's own, to edit. Import
  with `@/` (`@/components/ui/button`). Pre-rendered pages mustn't set `style` attributes: the
  Content-Security-Policy blocks inline styles in HTML.
- Server data goes through TanStack Query hooks (see `features/todos/todoQueries.ts`). After a
  change, invalidate the affected query keys.
- Call the API with the typed client in `lib/api/client.ts`. Never edit `lib/api/schema.d.ts` by
  hand; regenerate it with `npm run gen:api`.
<!--#if (Identity) -->
- Access tokens stay in memory (`lib/api/authTokens.ts`); don't move them to browser storage.
<!--#endif -->

## Tests

Client unit tests (Vitest and React Testing Library) sit next to the code they test
(`*.test.tsx`) and fake the API with MSW: add a handler with `server.use(http.get('*/api/...'))`,
and render pages with `renderPage`/`renderRoutes` from `src/test/render.tsx`. Unhandled requests
fail the test. Find elements the way users do (roles, labels, text), not by class names.
<!--#if (Identity) -->
`features/auth/testing.tsx` signs in, fakes `/api/auth/me`, and fakes the browser's passkey API.
<!--#endif -->
<!--#if (IncludeTests) -->

Integration tests in `tests/ApiHost.Tests` start a real PostgreSQL with Testcontainers and call
the API over HTTP with Alba. Add tests next to the existing ones for new endpoints. For messages,
wrap the call in `_fixture.Host.ExecuteAndWaitAsync(...)` and assert on the tracked session;
scheduled messages can be played immediately with `PlayScheduledMessagesAsync`.
<!--#if (Identity) -->
Account emails are captured (`_fixture.Emails`); follow their links with `LinkQuery`. New
endpoints must keep `EndpointSecurityTests` passing (login, roles, rate limiting).
<!--#endif -->

End-to-end tests in `tests/e2e/specs` use Playwright against the running app. Navigate with the
app's own links after logging in: `page.goto` reloads the page, which signs out (tokens are in
memory only).
<!--#endif -->

## Documentation

When a change alters behaviour, configuration or a security measure, update the matching page in
`docs/` (and the settings table in `docs/deployment.md` for a new setting) in the same change.

## Don't

- Commit `src/ApiHost/Internal/Generated/` (`dotnet publish` generates it) or `deploy/.env`.
- Add `ConfigureAwait(false)` (no effect in ASP.NET Core).
- Use `string.Equals` in LINQ queries; `==` is what Marten and EF Core translate.
