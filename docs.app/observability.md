# Observability

## Health checks

- `/health/live`: the process is running.
- `/health/ready`: it can also reach PostgreSQL.

Point your host's or load balancer's health checks at them.

## Traces, metrics and logs

The app records traces, metrics and logs with OpenTelemetry: HTTP requests, SQL commands,
<!--#if (IsMarten) -->
Wolverine messages and handlers, Marten, and .NET runtime metrics
<!--#else -->
Wolverine messages and handlers, and .NET runtime metrics
<!--#endif -->
(`src/ApiHost/Observability/OpenTelemetrySetup.cs`). Nothing is sent anywhere until you set
`OTEL_EXPORTER_OTLP_ENDPOINT`.

To see it locally in the Aspire dashboard:

```bash
docker compose --profile observability up -d     # dashboard on http://localhost:18888
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project src/ApiHost
```

(`dotnet run` itself also reports to the dashboard, as `dotnet-cli`; the app is `ApiHost`.)

In production, point `OTEL_EXPORTER_OTLP_ENDPOINT` (and, if needed, `OTEL_EXPORTER_OTLP_HEADERS`)
at any OpenTelemetry backend, such as Grafana, Honeycomb or a self-hosted Jaeger.
<!--#if (Identity) -->

## Failed emails

Account emails are sent by a Wolverine handler and retried with growing delays if the mail
server fails. Emails that still fail end up in Wolverine's `wolverine_dead_letters` table, and
the failures are in the logs.
<!--#endif -->
