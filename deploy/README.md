# Deploying ApiHost to a small server

This folder runs the app in production with Docker Compose: PostgreSQL, the app's container, and
[Caddy](https://caddyserver.com/) in front, which gets and renews an HTTPS certificate
automatically. A small VPS (1 vCPU, 1–2 GB of memory) from any provider is enough to start.

## What you need

- A Linux server with Docker and the Docker Compose plugin
  ([install guide](https://docs.docker.com/engine/install/)). Check with `docker compose version`.
- A domain name, with a DNS `A` (and, for IPv6, `AAAA`) record pointing at the server.
- Ports 80 and 443 open to the internet (Caddy needs port 80 to get the certificate). Keep every
  other port closed except SSH, for example with `ufw`:

  ```bash
  sudo ufw allow OpenSSH && sudo ufw allow 80,443/tcp && sudo ufw allow 443/udp && sudo ufw enable
  ```

## 1. Build the container image

On your machine (or in CI), from the repository root:

```bash
dotnet publish src/ApiHost -c Release /t:PublishContainer
```

This builds the React app, pre-generates Wolverine's code, and creates the image `apihost:latest`
in your local Docker, on Microsoft's ASP.NET Core runtime image. The app runs as a non-root user
on port 8080.

## 2. Get the image onto the server

Either copy it over SSH:

```bash
docker save apihost:latest | gzip | ssh you@your-server 'gunzip | docker load'
```

Or push it to a container registry and pull it there. For example, with the GitHub Container
Registry (after `docker login ghcr.io`):

```bash
dotnet publish src/ApiHost -c Release /t:PublishContainer \
  -p ContainerRegistry=ghcr.io -p ContainerRepository=your-org/apihost -p ContainerImageTag=1.0.0
```

and set `APP_IMAGE=ghcr.io/your-org/apihost:1.0.0` in `.env` on the server.

## 3. Configure and start it

Copy this folder to the server, create `.env` from the example, and fill it in:

```bash
scp -r deploy you@your-server:~/apihost
ssh you@your-server
cd ~/apihost
cp env.example .env
nano .env            # set DOMAIN and POSTGRES_PASSWORD (generate it with: openssl rand -base64 32)
<!--#if (Identity) -->
                     # and JWT_SIGNING_KEY (openssl rand -base64 48), plus SMTP_* for reset emails
<!--#endif -->
docker compose up -d
```

Open `https://your-domain`. The first request can take a few seconds while Caddy gets the
certificate. `.env` holds secrets: keep it on the server only (it's in `.gitignore`).

The app creates and updates its database tables when it starts.

## Everyday tasks

| Task | Command (in the folder on the server) |
|---|---|
| See what's running | `docker compose ps` |
| Follow the app's log | `docker compose logs -f app` |
| Deploy a new version | Build and copy the image again (steps 1–2), then `docker compose up -d` |
| Back up the database | `docker compose exec -T postgres pg_dump -U apihost apihost \| gzip > backup-$(date +%F).sql.gz` |
| Restore a backup | `gunzip -c backup.sql.gz \| docker compose exec -T postgres psql -U apihost apihost` (into an empty database) |
| Restart everything | `docker compose restart` |

Copy backups off the server, for example on a schedule with `cron` and `rsync` or your
provider's object storage. A backup that only lives on the same server isn't a backup.

## Health and monitoring

- Caddy only sends traffic to the app while `/health/ready` answers (the app is up and can reach
  PostgreSQL). Point an uptime monitor at `https://your-domain/health/ready` too.
- To collect traces, metrics and logs, set `OTEL_EXPORTER_OTLP_ENDPOINT` (and
  `OTEL_EXPORTER_OTLP_HEADERS` if your provider needs a key) in `.env`. Any OpenTelemetry backend
  works.

## Troubleshooting

- **No HTTPS certificate:** check that DNS for `DOMAIN` points at this server
  (`dig +short your-domain`), that ports 80 and 443 are open, and read
  `docker compose logs caddy`.
- **The app keeps restarting:** read `docker compose logs app`. Missing or wrong settings in `.env`
  are the usual cause; the app refuses to start rather than run misconfigured.
- **`PublishContainer` says it can't find Docker:** check `docker version` works without `sudo`.
  If you were just added to the `docker` group, log out and back in, then run
  `dotnet build-server shutdown` so the build processes pick up the new group.
