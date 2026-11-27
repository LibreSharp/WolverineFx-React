using JasperFx;
using JasperFx.CodeGeneration;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
#if (IsMarten)
using ApiHost.Data.Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
#endif
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;
using Wolverine.SignalR;
using ApiHost.ClientApp;
using ApiHost.Errors;
using ApiHost.Features.Todos;
using ApiHost.Observability;
using ApiHost.Realtime;
#if (Saas)
using ApiHost.Organisations;
#endif
using ApiHost.Security;
#if (Identity)
using ApiHost.Identity;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddErrorResponses();
builder.AddAppOpenTelemetry();

#if (IsMarten)
builder.Services.AddMartenPersistence(builder.Configuration);
#endif

builder.Host.UseWolverine(opts =>
{
#if (IsEfCore)
    opts.UseEfCorePersistence(builder.Configuration);

#endif
    // Commit the unit of work (Marten session / EF Core DbContext) after any handler or
    // endpoint that uses it, together with any messages it sent (transactional outbox).
    opts.Policies.AutoApplyTransactions();

    // Store messages on local queues in the database (the durable inbox/outbox), so a
    // cascaded message like TodoCompleted survives a crash or restart.
    opts.Policies.UseDurableLocalQueues();

    // Register the app's FluentValidation validators (e.g. CreateTodoValidator).
    opts.UseFluentValidation();

    // Realtime updates: AppHub is a SignalR hub that is also a Wolverine transport, and
    // TodoChanged and TodoReminder messages are pushed to connected browsers through it.
    opts.UseSignalR<AppHub>();
    opts.PublishMessage<TodoChanged>().ToSignalR();
    opts.PublishMessage<TodoReminder>().ToSignalR();
});

#if (IsEfCore)
// Apply EF Core migrations at startup (before anything else that uses the database).
builder.Services.AddHostedService<MigrateDatabaseOnStartup>();

#endif
// Code generation: Development (and the tests) generate Wolverine's handler code at
// runtime, so changes need no extra step. Production loads code that `dotnet publish`
// generated ahead of time (see the GenerateWolverineCode target in the .csproj), for a
// faster start; it fails at startup, not on the first request, if any of it is missing.
builder.Services.CritterStackDefaults(x =>
{
    x.Production.GeneratedCodeMode = TypeLoadMode.Static;
    x.Production.AssertAllPreGeneratedTypesExist = true;
});

builder.Services.AddWolverineHttp();

// Health checks for load balancers and container platforms: /health/live says the process is
// running; /health/ready also checks that PostgreSQL is reachable. The connection string is read
// when a check runs, so configuration supplied later (tests, environment variables) is used.
builder.Services.AddHealthChecks()
#if (IsMarten)
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Marten")!, name: "postgres", tags: ["ready"]);
#else
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")!, name: "postgres", tags: ["ready"]);
#endif

#if (Identity)
builder.Services.AddIdentityJwt(builder.Configuration, builder.Environment);
#endif

var app = builder.Build();

// Errors are ProblemDetails JSON with a trace ID (see Errors/ErrorResponses.cs).
app.UseErrorResponses();

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document (/openapi/v1.json; also used by the client's `npm run gen:api`), and
    // an interactive API reference to browse and try the endpoints at /scalar.
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Development (including the tests) runs over plain HTTP with no HTTPS port to redirect
// to; the middleware would only log a warning on every run.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    // Tells browsers to use HTTPS for this site from now on (sent over HTTPS only).
    app.UseHsts();
}

app.UseSecurityHeaders();

#if (Identity)
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
#endif

app.MapWolverineEndpoints(opts =>
{
    // Run a request's validator before the endpoint; failures return 400 ProblemDetails.
    opts.UseFluentValidationProblemDetailMiddleware();
#if (Saas)

    // Multi-tenancy: each request works in the organisation named in its access token. Wolverine
    // opens Marten sessions and EF Core DbContexts for that tenant, so tenanted data (Todos) is
    // filtered to it, and messages the request sends carry it on to their handlers.
    opts.TenantId.IsClaimTypeNamed(OrganisationClaims.OrganisationId);
#endif
});

app.MapWolverineSignalRHub<AppHub>(AppHub.Path);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Production: serve the built React app, with real 404s, robots.txt and sitemap.xml (see
// ClientApp/ClientAppHosting.cs). In Development the Vite dev server serves it.
if (!app.Environment.IsDevelopment())
{
    app.MapClientApp();
}

return await app.RunJasperFxCommands(args);

public partial class Program;
