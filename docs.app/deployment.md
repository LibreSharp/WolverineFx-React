# Deployment

`deploy/` has everything to run the app on a small server: a production `docker-compose.yml`
(PostgreSQL, the app's container, and Caddy for automatic HTTPS) and a step-by-step guide in
[deploy/README.md](../deploy/README.md). The container image is built with
`dotnet publish src/ApiHost -c Release /t:PublishContainer`.

The rest of this page applies wherever you run it.

## Publishing

`dotnet publish` builds the React app into `wwwroot` and pre-generates Wolverine's handler code,
so production starts quickly and compiles nothing at runtime.

```bash
dotnet publish src/ApiHost -c Release -o publish
cd publish                                         # run from here, so it finds wwwroot
ASPNETCORE_ENVIRONMENT=Production dotnet ApiHost.dll
```

<!--#if (IsEfCore) -->
EF Core migrations are applied when the app starts.
<!--#else -->
Marten creates and updates its tables when the app starts.
<!--#endif -->
For health checks, see [Observability](observability.md).

## What the published app serves

- The pages in `src/Client/src/pages.ts`: the public ones (`index: true`, such as `/`) as HTML
  the build pre-rendered, and the others as a page that starts the app. Any other path gets that
  page with status 404 (the app shows its not-found page), so search engines don't index made-up
  URLs. The HTML is sent with `Cache-Control: no-cache`, so browsers and CDNs pick up a new
  deployment at once.
- Built files under `/assets/`, with content hashes in their names, cached for a year.
- `/robots.txt` (everything may be crawled except `/api/`) and `/sitemap.xml` (the pages marked
  `index` in `pages.ts`), using the address the visitor used.
- Unknown `/api/` paths answer 404 as `ProblemDetails`; errors are `ProblemDetails` with a trace ID
  that matches the logs.

## Configuration

Set these with environment variables (or any ASP.NET Core configuration source):

| Setting | Example |
|---|---|
<!--#if (IsMarten) -->
| `ConnectionStrings__Marten` | `Host=db;Database=apihost;Username=app;Password=…` |
<!--#else -->
| `ConnectionStrings__Postgres` | `Host=db;Database=apihost;Username=app;Password=…` |
<!--#endif -->
| `ASPNETCORE_URLS` | `http://+:8080` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` behind a reverse proxy, so the app sees clients' real addresses and HTTPS |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optional: where to send traces, metrics and logs |
<!--#if (Identity) -->
| `Jwt__SigningKey` | A random secret of at least 32 bytes ([how to change it](how-to/rotate-the-signing-key.md)) |
| `App__ClientUrl` | The app's public address, such as `https://app.example.com`: links in emails use it |
| `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Security`, `Email__Smtp__UserName`, `Email__Smtp__Password`, `Email__Smtp__From` | Your email provider's SMTP settings ([how-to](how-to/configure-email.md)) |
| `Accounts__AdminEmails__0` | Your email: that account becomes an admin once its email is confirmed (more: `__1`, `__2`, ...) |
| `Accounts__AllowRegistration` | `false` to allow only invited people to create accounts (default `true`) |
| `Accounts__AuthAttemptsPerMinute` | Sign-in and account attempts allowed per client address per minute |
| `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret` | Optional: turns on "Continue with Google" ([how-to](how-to/set-up-external-sign-in.md)) |
| `Authentication__GitHub__ClientId`, `Authentication__GitHub__ClientSecret` | Optional: turns on "Continue with GitHub" |

The app refuses to start outside Development if `Jwt:SigningKey` is missing, too short, or still
the development key, or if `App:ClientUrl` isn't set. Email links (confirmation, password reset,
invitations) are built from `App:ClientUrl`, never from the request's `Host` header: that header
is chosen by whoever sends the request, so a password reset requested with a forged host would
email a real link to someone else's site. Also refuse requests for other host names: the Caddy
setup in `deploy/` only serves `DOMAIN`, and without such a proxy set `AllowedHosts` (for example
`AllowedHosts=app.example.com`; keep your load balancer's health check host in it too).

The demo user is never created outside Development. Without
`Email:Smtp:Host`, no emails are sent and the app logs a warning instead (never the link), and
since new accounts must confirm their email, nobody can finish signing up: configure SMTP before
people register.

ASP.NET Core Data Protection keys, which protect the tokens in account emails, are stored in
PostgreSQL, so email links keep working across restarts and on every instance of the app.
<!--#endif -->

## Running more than one instance

The app is built to run as **one instance**, which a small server handles comfortably. Some parts
already work across several instances, because they coordinate through PostgreSQL: Wolverine's
outbox and scheduled messages, and the Data Protection keys.

Two parts don't yet:

- **Live updates (SignalR):** a message only reaches browsers connected to the instance that sent
  it. Several instances need a SignalR backplane (for example Redis).
<!--#if (Identity) -->
- **Sign-in rate limits:** each instance counts attempts separately, so the limits are multiplied
  by the number of instances. Shared limits need a shared store (for example Redis).
<!--#else -->
- **In-memory state** you add yourself (caches, counters) is per instance.
<!--#endif -->

Behind a CDN such as Cloudflare, one instance goes a long way, since the built client is served
from the CDN's cache; see [Put Cloudflare in front](how-to/put-cloudflare-in-front.md).

Before going live, read the deployment checklist in [Security](security.md#when-you-deploy).
