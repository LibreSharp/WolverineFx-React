using JasperFx;
#if (IsMarten)
using Api.Data.Marten;
#elif (IsEfCore)
using Api.Data.EfCore;
#endif
using Wolverine;
using Wolverine.Http;
#if (Identity)
using Api.Identity;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

#if (IsMarten)
builder.Services.AddMartenPersistence(builder.Configuration);
builder.Host.UseWolverine();
#elif (IsEfCore)
builder.Host.UseEfCorePersistence(builder.Configuration);
#endif

builder.Services.AddWolverineHttp();

#if (Identity)
builder.Services.AddIdentityJwt(builder.Configuration);
#endif

var app = builder.Build();

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
