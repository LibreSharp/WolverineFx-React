# Testing

| Suite | Where | Run |
|---|---|---|
<!--#if (IncludeTests) -->
| API integration tests | `tests/ApiHost.Tests` | `dotnet test` (Docker must be running) |
<!--#endif -->
| Client unit tests | `src/Client/src/**/*.test.tsx` | `npm test` in `src/Client` (`npm run test:watch` while working) |
<!--#if (IncludeTests) -->
| End-to-end tests | `tests/e2e` | `npm install` once, then `npm test` in `tests/e2e`, with the app running |
<!--#endif -->
<!--#if (IncludeTests) -->

## API integration tests

The tests start their own throwaway PostgreSQL in Docker (Testcontainers), run the real app, and
call it over HTTP with [Alba](https://jasperfx.github.io/alba/). They cover:

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
- every endpoint's security attributes (`EndpointSecurityTests`).
<!--#endif -->

Writing one:

- Put it next to the existing tests, in the `[Collection("api")]` collection, which shares one
  app and database (`AppFixture`).
- For messages, wrap the call in `_fixture.Host.ExecuteAndWaitAsync(...)` and assert on the
  tracked session; scheduled messages can be played at once with `PlayScheduledMessagesAsync`.
<!--#if (Identity) -->
- `_fixture.CreateUserAndGetAccessTokenAsync()` gives you a signed-in user. Emails are captured
  in `_fixture.Emails`; follow a link with `_fixture.Emails.LinkQuery(email, "/confirm-email")`.
- A new endpoint under `/api/account/` or `/api/admin/` must require a login (and the Admin role
  for admin endpoints), and one that checks a password, sends email or creates an account must be
  rate limited, or `EndpointSecurityTests` fails. Add anonymous or rate-limited endpoints to its
  lists.
<!--#endif -->
<!--#endif -->

## Client unit tests

They use [Vitest](https://vitest.dev/) and React Testing Library in a simulated browser (jsdom),
with the API faked by [MSW](https://mswjs.io/) at the network level, so requests go through the
real API client. They sit next to the code they test and cover
<!--#if (Identity) -->
the Todo page, the API client's token refresh, the login, registration, email-link, invitation
and external sign-in pages, route guards, Settings and the admin page.
<!--#else -->
the Todo page and the API client.
<!--#endif -->

Writing one:

- Fake the API responses the test needs with `server.use(http.get('*/api/...', () => HttpResponse.json(...)))`.
  A request without a fake fails the test.
- Render with `renderPage` or `renderRoutes` from `src/test/render.tsx` (a fresh query cache and
  an in-memory router per test).
- Find elements the way users do (role, label, text), and interact with `userEvent`.
<!--#if (Identity) -->
- `features/auth/testing.tsx` has helpers to sign in, fake `/api/auth/me`, and fake the browser's
  passkey API (jsdom has none).
<!--#endif -->
<!--#if (IncludeTests) -->

## End-to-end tests

They drive the running app in Chrome with [Playwright](https://playwright.dev/), using the Chrome
installed on your machine (no browser download).
<!--#if (Identity) -->
They sign up and confirm accounts with the real emails in Mailpit, use Todos with live updates
across tabs, change settings, reset a password, trip the lockout, use the admin page and
invitations, and log in with a passkey (Chrome's virtual authenticator).

To run them against the Development setup (`docker compose up -d` for PostgreSQL and Mailpit),
start the API with a higher sign-in rate limit, since every test comes from your machine, and the
Vite dev server, then run them:

```bash
Accounts__AuthAttemptsPerMinute=1000 dotnet run --project src/ApiHost   # terminal 1
npm run dev                                                              # terminal 2, in src/Client
E2E_BASE_URL=http://localhost:5173 E2E_ADMIN_EMAIL=demo@example.com E2E_ADMIN_PASSWORD='Demo123!' npm test   # in tests/e2e
```

Against a published app instead, set `E2E_BASE_URL` to its address, `MAILPIT_URL` if Mailpit
isn't on `http://localhost:8025`, and list `E2E_ADMIN_EMAIL` (default `admin-e2e@example.com`) in
the app's `Accounts:AdminEmails`.

After logging in, navigate with the app's links: `page.goto` reloads the page, and a reload signs
out (tokens are kept in memory only).
<!--#else -->
They check the home page, and that Todos are added, completed and shown live in other tabs. Run
the API and the Vite dev server, then in `tests/e2e`: `E2E_BASE_URL=http://localhost:5173 npm test`.
<!--#endif -->

A failed test keeps a trace in `tests/e2e/test-results/`; open it with
`npx playwright show-trace <path>/trace.zip`.
<!--#endif -->
