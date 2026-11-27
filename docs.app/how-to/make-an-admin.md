# Make someone an admin

Admins manage users on the admin page (`/admin`). There are two ways to make someone an admin.

## From configuration (the first admin)

List the email under `Accounts:AdminEmails`. That account gets the Admin role once its email is
confirmed: when the app starts, or when they next log in. With environment variables:

```bash
Accounts__AdminEmails__0=you@example.com
Accounts__AdminEmails__1=colleague@example.com
```

With the production `docker-compose` in `deploy/`, set `ADMIN_EMAIL` in `.env`. Then register
with that email (or have them register), open the confirmation link, and log in: the header shows
an **Admin** link.

The email must be confirmed first, so nobody gets admin by registering someone else's address.
Removing an email from the list doesn't take the role away; do that on the admin page.

## From the admin page

An admin can tick **Admin** next to any user, or invite someone with the Admin role ticked.
Admins can't remove their own Admin role, so there's always at least one.

In Development, the demo user (`demo@example.com`) is an admin (`appsettings.Development.json`).
