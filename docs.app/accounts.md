# Accounts

Accounts use ASP.NET Core Identity, with the app's own JSON Web Tokens instead of cookies. The
endpoints are features, like the Todos: `src/ApiHost/Features/Auth/` (signing in and
registering), `Features/Account/` (the signed-in user's own account) and `Features/Admin/`. What
they share (users, tokens, roles, lockout, emails, providers and passkey set-up) is in
`src/ApiHost/Identity/`. The pages match: `src/Client/src/features/auth/`, `features/account/`
and `features/admin/`. For how each part protects users, see
[Security](security.md).

## Signing in

- **Tokens.** Login returns a short-lived access token (a JWT) and a refresh token. The client
  refreshes automatically when a request gets a 401; each refresh replaces the refresh token, and
  reusing an old one revokes the user's tokens. Tokens are kept in memory, not in browser
  storage, so reloading the page signs you out. Lifetimes are under `Jwt` in `appsettings.json`.
- **Ways to sign in:** email and password, a passkey, or Google/GitHub when configured.
<!--#if (Saas) -->
- **Data belongs to organisations.** Each user works in one organisation at a time (see
  [Organisations](#organisations)), and sees only its data.
<!--#else -->
- **Each user sees only their own data.** Endpoints scope queries to the signed-in user
  (`principal.GetUserId()`), and SignalR updates go only to that user's tabs.
<!--#endif -->

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
<!--#if (Saas) -->
handles it to delete that data. The user leaves each of their organisations, and one they were
the only member of is deleted with its data (`Organisations/LeaveOrganisationsOfDeletedAccountHandler.cs`).
Todos in organisations that others are still in stay: they belong to the organisation, not the
user, and keep the deleted user's id as their `OwnerId`, in the Todo and in its event history.
Nothing else about the user (email, name) is kept with them. If your data needs to forget its
authors too, clear or replace those ids in an `AccountDeleted` handler.
<!--#else -->
handles it to delete that data (see `Features/Todos/DeleteTodosOfDeletedAccountHandler.cs`).
<!--#endif -->

## Notifications

The bell in the header lists the user's latest notifications, such as a Todo reminder that came
due, with the number unread. They arrive live (over the realtime connection) and are kept per
user (`Notifications:KeepPerUser`, 100). Each one is also emailed, unless the user turned that
kind off in Settings ("Email notifications") or with the link at the bottom of the email. The link
needs no login, and mail apps show a one-click "Unsubscribe" button for it (the
`List-Unsubscribe` headers). Account emails, such as password resets, can't be turned off.

To notify someone, publish `SendNotification(userId, category, title, body, link)` from an
endpoint or handler (it goes through the outbox). Add new kinds to `NotificationCategories` in
`Notifications/Notification.cs`, with the description users see in Settings. The reminder handler
(`Features/Todos/RemindTodoEndpoint.cs`) is an example.

## Download your data

Settings has "Download your data": everything the app stores about the user as one JSON file
(their account, sign-ins and passkeys, notifications and email choices,
<!--#if (Saas) -->
and the organisations they belong to). Organisation owners download the organisation's data
from the organisation page: its Todos, members, billing and integrations.
<!--#else -->
and their Todos).
<!--#endif -->
Privacy laws such as the GDPR and Thailand's PDPA give people this right. Never the password hash
or other secrets.

A feature that stores data about users adds its section by registering an `IPersonalDataSource`
(`DataExport/DataExport.cs`), like `NotificationsPersonalData`.
<!--#if (Saas) -->
A feature that stores organisation data registers an `IOrganisationDataSource`, like
`TodosOrganisationData`.
<!--#endif -->

## Lockout

Repeated wrong passwords lock the account for a while (ASP.NET Core Identity's lockout,
configured in `ConfigureIdentity` in `Identity/IdentityJwtSetup.cs`). While locked, login is
refused without checking the password, refresh tokens stop working, and resetting the password
lifts the lock. An admin can also lock an account until they unlock it.

## Roles and the back office

Roles are constants in `Identity/AppRoles.cs`, created in the database at startup
(`RoleSeeder`). Protect endpoints with `[Authorize(Roles = AppRoles.Admin)]`. A changed role
reaches the user's access token at their next token refresh.

The back office (`/admin`, Admin role only) has these sections:

- **Users:** search users, change their roles, lock and unlock accounts (an admin lock ends the
  user's sessions), end a user's sessions (sign them out everywhere: their refresh tokens are
  revoked, and each session ends when its access token expires), resend the confirmation email,
  and invite people. An invitation creates the account and emails a link to choose a password,
  valid for a week.
<!--#if (Saas) -->
  Each user's organisations are listed too.
- **Dashboard, Organisations and Feature flags:** see [Plans, trials and limits](billing.md#the-back-office).
<!--#endif -->
- **Audit log:** every change an admin makes, with who made it and when
  (`Features/Admin/IAuditLog.cs`). Record new admin actions with `IAuditLog.RecordAsync`.
- **Failed messages:** background work that failed every attempt and waits in Wolverine's
  dead-letter queue, such as an email the SMTP server refused. Fix the cause, then replay the
  message (it runs again) or discard it.

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

<!--#if (Saas) -->
## Organisations

Data belongs to organisations, the app's tenants. The code is in `src/ApiHost/Organisations/`
(the model, its store, and choosing a session's organisation) and `Features/Organisations/`
(the endpoints); the pages are in `src/Client/src/features/organisations/`.

- **Everyone has one.** A new account gets its own organisation, named after the user. Users can
  create more, and be invited into others; the switcher in the header picks the current one.
- **The access token names the organisation** (the `org` claim), chosen when the token is issued:
  the one the session was in, or the one the user used last. Switching issues new tokens.
  Every request checks that the user is still a member, so removing someone, or deleting the
  organisation, takes effect at once; the browser then refreshes its tokens into another of the
  user's organisations.
- **Multi-tenancy.** Wolverine reads the claim as each request's tenant id and carries it on the
  messages the request sends. Tenanted data (`Todo`, which implements `ITenanted`) is stored with
  a tenant id
<!--#if (IsMarten) -->
  (Marten's conjoined multi-tenancy),
<!--#else -->
  (Wolverine's conjoined multi-tenancy for EF Core: a `tenant_id` column and a global query filter),
<!--#endif -->
  so queries only ever see the current organisation's, without a filter in each endpoint.
  Organisations, memberships and invitations themselves aren't tenanted.
- **Roles:** Owner, Admin and Member. Owners and admins invite and remove members and change
  roles, but only owners make owners, change an owner's role, rename or delete the organisation.
  There's always an owner: the last one can't leave or step down, and can't delete their account
  while others are members (`Features/Organisations/OrganisationRules.cs`). Each change checks
  again after saving and undoes itself if a change at the same moment left no owner; and if an
  owner's account is deleted after someone joined, their longest-standing admin (or member)
  becomes the owner.
- **Invitations** go by email, with a link to `/join` that works for a week. Someone with an
  account logs in with the invited email and accepts; someone without one chooses a password,
  and the link confirms their email. Only a hash of the link's token is stored, and the page
  sends the token in request bodies, never in a URL the API would log. Each user can send 20
  invitations an hour and an organisation can have 100 waiting (`Organisations__InvitationsPerHour`,
  `Organisations__MaxPendingInvitations`; see `Organisations/OrganisationOptions.cs`), so the app
  can't be used to send spam.
- **Deleting an organisation** deletes it for everyone. Its data is deleted in the background:
  each feature handles `OrganisationDeleted`, which runs for that organisation's tenant (see
  `Features/Todos/DeleteTodosOfDeletedOrganisationHandler.cs`). The message is published in the
  same transaction as the deletion, so the data is never left behind.
- **Realtime updates** go to everyone working in the organisation; the browser reconnects when it
  switches. Reminders go only to the member who set them, while they're still a member. A
  connection is closed when its access token expires, so someone removed from an organisation
  stops getting its updates within one token lifetime.

<!--#endif -->
## Development

The demo user (`demo@example.com` / `Demo123!`) is created at startup in Development only, is
already confirmed, and is an admin. Change it under `DevelopmentUser` in
`src/ApiHost/appsettings.Development.json` and in `src/Client/.env.development`.
