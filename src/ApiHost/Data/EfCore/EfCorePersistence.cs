#if (Saas)
using JasperFx;
#endif
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

#if (Saas)
        // Organisations: every ITenanted entity (Todo) gets a tenant_id column, and each DbContext
        // Wolverine creates for a request or message is pinned to its organisation: queries are
        // filtered to it, new rows are stamped with it, and changes to another's rows are refused.
        opts.Services.AddDbContextWithWolverineManagedConjoinedTenancy<AppDbContext>(
            (x, connection) => x.UseNpgsql(connection.Value), AutoCreate.None);
#else
        opts.Services.AddDbContextWithWolverineIntegration<AppDbContext>(
            x => x.UseNpgsql(connectionString));
#endif

        opts.UseEntityFrameworkCoreTransactions();
    }
}
