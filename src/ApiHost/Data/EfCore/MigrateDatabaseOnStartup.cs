using Microsoft.EntityFrameworkCore;

namespace ApiHost.Data.EfCore;

// Applies any pending EF Core migrations when the app starts. A hosted service (rather than
// code at the top of Program.cs) only runs when the app actually starts, so command-line
// tools such as `dotnet run -- codegen write` (run by `dotnet publish`) don't need a database.
// Registered before other hosted services that use the database, such as the development
// user seeder.
public class MigrateDatabaseOnStartup(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
