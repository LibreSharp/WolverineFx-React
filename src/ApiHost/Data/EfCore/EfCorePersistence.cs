using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace ApiHost.Data.EfCore;

public static class EfCorePersistence
{
    // Called from the UseWolverine(...) block in Program.cs.
    public static void UseEfCorePersistence(this WolverineOptions opts, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing 'Postgres' connection string");

        // Wolverine's inbox/outbox tables live in the same Postgres database.
        opts.PersistMessagesWithPostgresql(connectionString);

        opts.Services.AddDbContextWithWolverineIntegration<AppDbContext>(
            x => x.UseNpgsql(connectionString));

        opts.UseEntityFrameworkCoreTransactions();
    }
}
