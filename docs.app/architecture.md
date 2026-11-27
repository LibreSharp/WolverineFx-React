# Architecture

The Todo feature is a working example of each pattern. Copy it when you add your own features
(see [Add a feature](how-to/add-a-feature.md)).

## Backend

- **Endpoints.** Each endpoint is a static method with `[WolverineGet]` or `[WolverinePost]`.
  Wolverine finds it automatically and injects what it asks for. See `src/ApiHost/Features/Todos/`.
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
  schedules a `TodoReminderDue` message for later (`Features/Todos/RemindTodoEndpoint.cs`). Scheduled
  messages are stored in PostgreSQL, so reminders still fire after a restart. When it's due,
  `TodoReminderDueHandler` pushes a `todo-reminder` to the browser, unless the Todo was completed
  in the meantime.
- **Realtime updates.** Creating or completing a Todo sends a `todo-changed` message to the
  browser over SignalR (`TodoChanged`, routed in `Program.cs`), and every open tab refetches.
<!--#if (Identity) -->
  Each user only receives updates about their own Todos.
<!--#endif -->
- **Code generation.** Wolverine generates code for each endpoint and handler. In Development
  it does so at runtime, so changes need no extra step; `dotnet publish` generates it ahead of
  time and Production loads it ready-made, failing at startup if any is missing.
<!--#if (Identity) -->
- **Accounts.** ASP.NET Core Identity with JWT access tokens and rotating refresh tokens; see
  [Accounts](accounts.md).
<!--#endif -->

## Frontend

- **Routes** are in `src/Client/src/router.tsx` (React Router), with a shared layout (header,
  navigation, footer) in `components/Layout.tsx`. Pages other than the home page load when first
  visited (`lazy`), so the first page view downloads less JavaScript.
- **Page titles and search engines.** Each route's `handle` sets its page title and whether search
  engines may index it (`components/PageMeta.tsx`); only the home page may by default, and every
  other page gets `noindex`. `src/routes.ts` lists every page's path: the build writes it into
  `client-routes.json`, from which the API answers 404 for other paths and builds `sitemap.xml`.
  A unit test checks it matches the router. The default description and link-preview tags are in
  `index.html`: describe your product there.
- **Server data.** TanStack Query caches the Todo list (`features/todos/todoQueries.ts`).
  Changes and SignalR messages invalidate it, which refetches it.
- **The API client** (`lib/api/client.ts`) is typed from the API's OpenAPI document
  (`lib/api/schema.d.ts`, regenerated with `npm run gen:api` while the API runs).
<!--#if (Identity) -->
  It adds the access token to each request and, on a 401, refreshes the tokens once and retries.
<!--#endif -->
- **One deployable.** In development, the Vite dev server forwards `/api` (including the SignalR
  WebSocket) to the API, so the browser talks to one origin and no CORS is needed. In
  production, the API serves the built client from `wwwroot`.
