using JasperFx;
#if (IsMarten)
using ApiHost.Data.Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
#endif
using Wolverine;
using Wolverine.Http;
#if (Identity)
using ApiHost.Identity;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

#if (IsMarten)
builder.Services.AddMartenPersistence(builder.Configuration);
builder.Host.UseWolverine(opts =>
{
    // Commit the IDocumentSession after any handler/endpoint that uses it.
    opts.Policies.AutoApplyTransactions();
});
#elif (IsEfCore)
builder.Host.UseEfCorePersistence(builder.Configuration);
#endif

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

app.MapWolverineEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.MapStaticAssets();
    app.MapFallbackToFile("index.html");
}

return await app.RunJasperFxCommands(args);

public partial class Program;
