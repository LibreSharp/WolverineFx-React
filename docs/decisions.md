# Decisions

Why the template does things the way it does. Each entry says what was decided and why, so a
change can start from the reasons rather than rediscovering them.

## Stack

- **Free, open-source dependencies only.** The template exists to show a .NET stack with no
  commercial licences (see LibreSharp). JasperFx's paid tools and commercial UI kits stay out.
- **Few options.** Each yes/no option doubles the combinations CI must build and test; features
  that fit every project go in without one. v0.4 replaces `--Identity` with `--Auth
  none|identity|saas` rather than adding another yes/no option.
- **One deployable.** The API serves the built React app in production, so there's one
  container, one origin (no CORS) and one thing to deploy. The Vite dev server proxies `/api` in
  development for the same reason.
- **Pre-generated Wolverine code in production** (`TypeLoadMode.Static`): a faster start, no
  runtime compilation, and missing code fails at startup rather than on a request.

## Accounts

- **JWT in memory, not cookies or browser storage.** The API is a bearer-token API; keeping tokens
  in memory means injected scripts can't read them from storage, at the cost of a reload signing
  you out. A `sessionStorage` option waits for demand. Refresh tokens rotate, are stored hashed,
  and reuse revokes them all.
- **HS256 signing.** One shared secret is enough while only this API validates its tokens; RS256
  with a JWKS endpoint waits for a second service that needs to.
- **ASP.NET Core Identity for the rules.** Lockout, tokens, password rules and passkeys use
  Identity (`UserManager`, `SignInManager.CheckPasswordSignInAsync`, `IPasskeyHandler`) rather
  than reimplementing them. A locked-out login is told so without checking the password, as
  Identity does: checking it would give an attacker free guesses during the lockout.
- **Marten: subclass Marten.AspNetIdentity's user store.** Its store lacks lockout, security
  stamps, roles, external logins and passkeys. `ApplicationUserStore` adds them on the user
  document, keeping the package rather than replacing it.
- **Email through the outbox, tokens made at send time.** Endpoints return messages, so emails
  survive restarts and are retried; the handler creates the one-time token when sending, so no
  token is ever stored in the message tables.
- **Email confirmation required to log in,** with Mailpit in development so it costs nothing to
  try. Password reset also confirms the email (it proves ownership).
- **Roles are constants, seeded at startup.** A role does nothing until code checks it, so roles
  are added in code; the database still holds them, so editable roles would only need endpoints
  and a form later. First admins come from `Accounts:AdminEmails`, granted only once that email
  is confirmed.
- **External sign-in trusts verified emails only,** checks GitHub's email list itself (its handler
  doesn't check verification), and removes an unconfirmed account's password before linking
  (pre-hijacking). Tokens reach the single-page app in the URL fragment as a refresh token,
  exchanged at once, so rotation makes it single-use.
- **Passkeys without SignInManager's cookie.** Identity's `IPasskeyHandler` returns the ceremony
  state as a string; the API encrypts it (Data Protection, time-limited, bound to the user when
  registering) for the client to send back.

## Tests

- **Real PostgreSQL in tests** (Testcontainers), and the API driven over HTTP (Alba), so one test
  project covers both persistence options.
- **Endpoint security checked from metadata** (`EndpointSecurityTests`), so a new endpoint that
  forgets `[Authorize]`, a role or rate limiting fails a test.
- **Client tests fake the network (MSW),** not modules, so requests go through the real API
  client; unhandled requests fail.
- **End-to-end tests against the published app** with real email (Mailpit), in the installed
  Chrome, with a software or virtual authenticator for passkeys.
