# Send email through your email provider

The app sends account emails (confirmation, password reset, invitations, email changes) over
SMTP with MailKit. In Development they go to Mailpit; in production, point the app at your email
provider's SMTP service. Until you do, no emails are sent and nobody can finish signing up.

## Settings

| Setting | Meaning |
|---|---|
| `Email__Smtp__Host` | The provider's SMTP server. Setting it turns sending on. |
| `Email__Smtp__Port` | Usually `587` (or `465` with `SslOnConnect`). Default `587`. |
| `Email__Smtp__Security` | `Auto` (default: TLS when offered), `StartTls` (require TLS on 587), `SslOnConnect` (port 465), `None` (local test servers only) |
| `Email__Smtp__UserName`, `Email__Smtp__Password` | The SMTP credentials your provider gives you |
| `Email__Smtp__From` | The sender, as `no-reply@yourdomain.com` or `Your App <no-reply@yourdomain.com>` |

With the production `docker-compose` in `deploy/`, set the `SMTP_*` values in `.env` instead.

## Examples

Use the values your provider's dashboard shows; these are typical.

| Provider | Host | Credentials |
|---|---|---|
| Amazon SES | `email-smtp.<region>.amazonaws.com`, port 587 | SES SMTP credentials (created in the SES console; not your AWS access keys) |
| Postmark | `smtp.postmarkapp.com`, port 587 | Your server API token as both user name and password |
| Mailgun | `smtp.mailgun.org` (or `smtp.eu.mailgun.org`), port 587 | The domain's SMTP user and password |
| Brevo | `smtp-relay.brevo.com`, port 587 | Your SMTP login and key |

## Get your email delivered

- Send from a domain you own, and verify it with the provider.
- Add the SPF, DKIM and DMARC DNS records the provider gives you; without them, many emails end up
  as spam or are rejected.
- Send a password reset to yourself and check it arrives, links to your site, and isn't in spam.

## When sending fails

Emails are sent in the background and retried with growing delays. Emails that still fail are
kept in Wolverine's `wolverine_dead_letters` table, and the errors are in the logs. Users can
also ask again ("Send the confirmation email again" on the login page, or "Forgot your
password?").

To change what the emails say, edit `src/ApiHost/Identity/Email/AccountEmails.cs`. To use a
provider's HTTP API instead of SMTP, implement `IEmailSender` and register it in place of
`SmtpEmailSender` in `Identity/IdentityJwtSetup.cs`.
