# Accounts

Accounts use ASP.NET Core Identity, with the app's own JSON Web Tokens instead of cookies. The
code is in `src/ApiHost/Identity/` and the pages in `src/Client/src/features/auth/`,
`features/account/` and `features/admin/`. For how each part protects users, see
[Security](security.md).

## Signing in

- **Tokens.** Login returns a short-lived access token (a JWT) and a refresh token. The client
  refreshes automatically when a request gets a 401; each refresh replaces the refresh token, and
  reusing an old one revokes the user's tokens. Tokens are kept in memory, not in browser
  storage, so reloading the page signs you out. Lifetimes are under `Jwt` in `appsettings.json`.
- **Ways to sign in:** email and password, a passkey, or Google/GitHub when configured.
- **Each user sees only their own data.** Endpoints scope queries to the signed-in user
  (`principal.GetUserId()`), and SignalR updates go only to that user's tabs.

## Account emails

- **Confirmation.** Registering emails a link to `/confirm-email`, and login is refused (with a
  "send it again" button) until it's opened.
- **Email changes.** A new email in Settings is only used once the link sent to it is opened.
- **Password reset.** `/forgot-password` emails a link to `/reset-password`. Resetting also
  confirms the email and lifts a lockout from failed logins.
- **How they're sent.** Endpoints don't send email themselves: they return a message
  (`SendEmailConfirmation`, `SendPasswordReset`, ...) that Wolverine stores in its durable inbox,
  and `AccountEmailHandler` (`Identity/Email/AccountEmails.cs`) sends it in the background with
  MailKit, retrying if the mail server is down. The handler creates the one-time token when it
  sends, so tokens are never stored. To change the wording, edit the handler.
- **In Development** the emails go to Mailpit (http://localhost:8025). For production, see
  [Send email through your email provider](how-to/configure-email.md).

## Settings

At `/settings`, users change their display name and email, change their password (which signs
out their other sessions), add, rename and remove passkeys, link and unlink Google/GitHub, and
delete their account (password required). Settings won't remove the last way a user can sign in.

Deleting an account publishes an `AccountDeleted` message; each feature that stores user data
handles it to delete that data (see `Features/Todos/DeleteTodosOfDeletedAccountHandler.cs`).

## Lockout

Repeated wrong passwords lock the account for a while (ASP.NET Core Identity's lockout,
configured in `ConfigureIdentity` in `Identity/IdentityJwtSetup.cs`). While locked, login is
refused without checking the password, refresh tokens stop working, and resetting the password
lifts the lock. An admin can also lock an account until they unlock it.

## Roles and the admin page

Roles are constants in `Identity/AppRoles.cs`, created in the database at startup
(`RoleSeeder`). Protect endpoints with `[Authorize(Roles = AppRoles.Admin)]`. A changed role
reaches the user's access token at their next token refresh.

The admin page (`/admin`, Admin role only) lists and searches users, changes their roles, locks
and unlocks accounts (an admin lock ends the user's sessions), and invites people: an invitation
creates the account and emails a link to choose a password, valid for a week.

- The first admins come from configuration: see [Make someone an admin](how-to/make-an-admin.md).
- `Accounts:AllowRegistration=false` makes accounts invitation-only.
- To add a role, see [Add a role](how-to/add-a-role.md).

## Google and GitHub

Each provider is turned on by setting its client ID and secret; see
[Set up external sign-in](how-to/set-up-external-sign-in.md). The provider must vouch for the
email address: a verified email signs into (and links to) the account with that email, or creates
one. After the provider, the API redirects to `/external-login` with a refresh token in the URL
fragment, which the client exchanges for new tokens at once.

## Passkeys

Users add passkeys in Settings and can then use "Log in with a passkey": the browser offers the
passkeys it has for the site, so no email is typed. This is ASP.NET Core Identity's own WebAuthn
support (`IPasskeyHandler`), used without cookies: the ceremony's state is encrypted and handed to
the client for a few minutes (`Identity/Passkeys/PasskeySetup.cs`).

Passkeys belong to the site's host name: a passkey created on one domain doesn't work on another,
so moving the app to a new domain means users add new passkeys. In development, passkeys work on
`localhost`.

## Development

The demo user (`demo@example.com` / `Demo123!`) is created at startup in Development only, is
already confirmed, and is an admin. Change it under `DevelopmentUser` in
`src/ApiHost/appsettings.Development.json` and in `src/Client/.env.development`.
