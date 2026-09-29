using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace ApiHost.Data.EfCore;

public static class EfCorePersistence
{
    public static void UseEfCorePersistence(this IHostBuilder host, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing 'Postgres' connection string");

        host.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(connectionString);

            opts.Services.AddDbContextWithWolverineIntegration<AppDbContext>(
                x => x.UseNpgsql(connectionString));

            opts.UseEntityFrameworkCoreTransactions();

            // Call SaveChangesAsync after any handler/endpoint that uses AppDbContext.
            opts.Policies.AutoApplyTransactions();
        });
    }
}
