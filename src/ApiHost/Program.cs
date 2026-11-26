using JasperFx;
using JasperFx.CodeGeneration;
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
using ApiHost.Features.Todos;
using ApiHost.Realtime;
#if (Identity)
using ApiHost.Identity;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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
    // TodoChanged messages are pushed to connected browsers through it.
    opts.UseSignalR<AppHub>();
    opts.PublishMessage<TodoChanged>().ToSignalR();
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

#if (Identity)
builder.Services.AddIdentityJwt(builder.Configuration, builder.Environment);
#endif

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Development (including the tests) runs over plain HTTP with no HTTPS port to redirect
// to; the middleware would only log a warning on every run.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

#if (Identity)
app.UseAuthentication();
app.UseAuthorization();
#endif

app.MapWolverineEndpoints(opts =>
{
    // Run a request's validator before the endpoint; failures return 400 ProblemDetails.
    opts.UseFluentValidationProblemDetailMiddleware();
});

app.MapWolverineSignalRHub<AppHub>(AppHub.Path);

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.MapStaticAssets();
    // Unknown paths serve the React app, so client-side routes such as /login load directly;
    // unknown API paths still get a 404 rather than the app's HTML.
    app.MapFallback("/api/{**path}", () => Results.NotFound());
    app.MapFallbackToFile("index.html");
}

return await app.RunJasperFxCommands(args);

public partial class Program;
