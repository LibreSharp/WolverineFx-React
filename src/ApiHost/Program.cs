using JasperFx;
#if (IsMarten)
using ApiHost.Data.Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
#endif
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;
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
});

builder.Services.AddWolverineHttp();

#if (Identity)
builder.Services.AddIdentityJwt(builder.Configuration, builder.Environment);
#endif

var app = builder.Build();

#if (IsEfCore)
using (var migrationScope = app.Services.CreateScope())
{
    migrationScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}
#endif

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

#if (Identity)
app.UseAuthentication();
app.UseAuthorization();
#endif

app.MapWolverineEndpoints(opts =>
{
    // Run a request's validator before the endpoint; failures return 400 ProblemDetails.
    opts.UseFluentValidationProblemDetailMiddleware();
});

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.MapStaticAssets();
    app.MapFallbackToFile("index.html");
}

return await app.RunJasperFxCommands(args);

public partial class Program;
