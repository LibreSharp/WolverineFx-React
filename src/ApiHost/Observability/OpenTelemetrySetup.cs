using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiHost.Observability;

public static class OpenTelemetrySetup
{
    // Traces, metrics and logs with OpenTelemetry: HTTP requests, outgoing HTTP calls, SQL
    // commands (Npgsql), Wolverine messages and handlers, Marten, and .NET runtime metrics.
    // Nothing is sent anywhere unless OTEL_EXPORTER_OTLP_ENDPOINT is set; then everything goes to
    // that OTLP endpoint (Grafana, Honeycomb, Jaeger, the Aspire dashboard, and others accept it).
    // The other standard OTEL_* settings (headers, protocol, service name) work too.
    public static void AddAppOpenTelemetry(this WebApplicationBuilder builder)
    {
        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithTracing(tracing => tracing
                // Skip health check requests: frequent, and not interesting.
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation()
                .AddSource("Wolverine", "Marten", "Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Wolverine:*", "Marten", "Npgsql"))
            .WithLogging();

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            openTelemetry.UseOtlpExporter();
        }
    }
}
