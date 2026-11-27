# Security

What the app does to protect its users and data, and what's left to you when you deploy it.

## Built in

- **Browser security headers** on every response: a Content-Security-Policy that only allows
  scripts, styles and connections from the app's own origin, plus protections against MIME
  sniffing, framing (clickjacking) and leaking URLs in the Referer header
  (`src/ApiHost/Security/SecurityHeaders.cs`). The app's pages start with a few inline scripts
  that the build writes; the policy allows exactly those, by their hashes, and no others
  (`src/ApiHost/ClientApp/ClientAppHosting.cs`). If you add a third-party script, font or
  analytics service, add its origin to the policy.
- **HTTPS only in production:** requests are redirected to HTTPS and browsers are told to keep
  using it (HSTS).
- **Development-only tools stay out of production:** the OpenAPI document and the API reference
  page (`/scalar`) are only served in Development, and the React app's scripts are served from the
  app itself, not a CDN.
- **No runtime code compilation in production:** Wolverine's code is generated at publish time
  and the app fails at startup if any of it is missing.
<!--#if (Identity) -->

## Accounts and sign-in

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
  never stored in the database's message tables or written to logs. The links point at
  `App:ClientUrl`, never at the request's `Host` header, so a forged host can't redirect them
  (the app won't start without the setting outside Development).
- **No account enumeration where it matters:** "forgot password" and "resend confirmation" answer
  the same whether or not an account exists, and a login only says an email is unconfirmed to
  someone who knows the password.
- **Account lockout** (ASP.NET Core Identity's): repeated wrong passwords lock the account for a
  while, whichever addresses they come from. A locked account can't refresh its tokens. A password
  reset lifts a lockout from failed logins. Tune it in `src/ApiHost/Identity/IdentityJwtSetup.cs`.
- **Rate limiting:** endpoints that check passwords, send email or create accounts allow a small
  number of attempts per client address per minute (`src/ApiHost/Identity/AuthRateLimiting.cs`,
  and `Accounts:AuthAttemptsPerMinute`).
- **Sessions end when they should:** changing or resetting a password, deleting the account, and
  an admin lock all revoke the user's refresh tokens.
<!--#if (Saas) -->
- **Each organisation's data is kept apart** by multi-tenancy in the data layer, not by filters in
  each endpoint, so a forgotten filter can't leak another organisation's data; its records answer
  404. A token is only accepted while its user is still a member of its organisation. Deleting an
  organisation deletes its data, including Todo event history; deleting an account deletes the
  organisations the user was alone in.
- **Organisation roles** are checked on every change (only owners make owners or delete the
  organisation), and invitation links are single-use, expire after a week, and are stored only
  as a hash. Invitations are limited per user per hour and per organisation, so the app can't be
  used to send spam.
- **API keys** are stored only as a hash, shown once, scoped (read or write), rate limited per key,
  and refused everywhere but the paths they're for (no account, organisation, billing or admin
  endpoints). They stop at once when revoked, when their creator leaves, or when the
  organisation is suspended ([integrations](integrations.md#api-keys)).
- **Webhooks can't reach your network:** in production the sender refuses localhost, private and
  link-local addresses (including cloud metadata services), checked after DNS, and never follows
  redirects. Deliveries are signed, so receivers can check them.
- **Customers' secrets** (and webhook signing secrets) are encrypted with Data Protection and
  never sent back by the API. Back up the Data Protection keys with the database.
<!--#else -->
- **Each user sees only their own data:** another user's records answer 404, so their existence
  isn't revealed. Deleting an account (which needs the password again) deletes the user's data
  too, including Todo event history.
<!--#endif -->

## Roles and the admin page

- Roles are checked by the API on every admin request; hiding the admin page in the browser is
  only for convenience.
- Admins can't lock their own account or remove their own Admin role, so an admin always remains.
- `Accounts:AdminEmails` only grants Admin once that email has been confirmed, so nobody can claim
  it by registering the address first.
- Registration can be made invitation-only (`Accounts:AllowRegistration=false`).

## Google, GitHub and passkeys

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

## When you deploy

- Keep secrets out of source control: use environment variables, `deploy/.env` (on the server
  only) or a secret store.
- Expose only the reverse proxy. In `deploy/docker-compose.yml` only Caddy is reachable; the app
  and database aren't.
<!--#if (Identity) -->
- Set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` behind a reverse proxy (the production compose
  file does), so rate limits apply to each client's real address and HTTPS is detected.
- Use a long random `Jwt:SigningKey`, set `App:ClientUrl` to the app's public address, configure
  SMTP before people register, and set `Accounts:AdminEmails`.
- ASP.NET Core Data Protection keys (which protect the tokens in emails, link tickets and passkey
  state) are stored in PostgreSQL so they work across restarts and instances. Like the rest of
  your data they're unencrypted at rest: protect database access and backups.
<!--#endif -->
- Keep dependencies current: `.github/dependabot.yml` opens weekly update pull requests.
