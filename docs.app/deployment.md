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
| `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Security`, `Email__Smtp__UserName`, `Email__Smtp__Password`, `Email__Smtp__From` | Your email provider's SMTP settings ([how-to](how-to/configure-email.md)) |
| `Accounts__AdminEmails__0` | Your email: that account becomes an admin once its email is confirmed (more: `__1`, `__2`, ...) |
| `Accounts__AllowRegistration` | `false` to allow only invited people to create accounts (default `true`) |
| `Accounts__AuthAttemptsPerMinute` | Sign-in and account attempts allowed per client address per minute |
| `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret` | Optional: turns on "Continue with Google" ([how-to](how-to/set-up-external-sign-in.md)) |
| `Authentication__GitHub__ClientId`, `Authentication__GitHub__ClientSecret` | Optional: turns on "Continue with GitHub" |

The app refuses to start outside Development if `Jwt:SigningKey` is missing, too short, or still
the development key. The demo user is never created outside Development. Without
`Email:Smtp:Host`, no emails are sent and the app logs a warning instead (never the link), and
since new accounts must confirm their email, nobody can finish signing up: configure SMTP before
people register.

ASP.NET Core Data Protection keys, which protect the tokens in account emails, are stored in
PostgreSQL, so email links keep working across restarts and on every instance of the app.
<!--#endif -->

Before going live, read the deployment checklist in [Security](security.md#when-you-deploy).
