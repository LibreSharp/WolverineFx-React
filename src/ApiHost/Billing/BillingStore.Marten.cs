using Marten;

namespace ApiHost.Billing;

// Billing accounts are ordinary (not tenanted) Marten documents. PerPeriod usage is a plain table,
// billing_usage (Data/Marten/MartenPersistence.cs), so it can be added to in one SQL statement.
public class BillingStore(IDocumentSession session) : IBillingStore
{
    public const string UsageTable = "billing_usage";

    public Task<BillingAccount?> FindAccountAsync(Guid organisationId, CancellationToken cancellationToken)
        => session.LoadAsync<BillingAccount>(organisationId, cancellationToken);

    public Task SaveAccountAsync(BillingAccount account, CancellationToken cancellationToken)
    {
        session.Store(account);
        return session.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> UsageAsync(Guid organisationId, string limit, DateTimeOffset periodStart, CancellationToken cancellationToken)
    {
        var rows = await session.AdvancedSql.QueryAsync<int>(
            $"select used from {session.DocumentStore.Options.DatabaseSchemaName}.{UsageTable} where organisation_id = ? and limit_name = ? and period_start = ?",
            cancellationToken,
            organisationId, limit, periodStart.ToUniversalTime());
        return rows.FirstOrDefault();
    }

    public async Task<bool> TryUseAsync(Guid organisationId, string limit, DateTimeOffset periodStart, int amount, int max, CancellationToken cancellationToken)
    {
        // Inserts the period's row, or adds to it only while the total stays within max. No row
        // comes back when it would go over.
        var table = $"{session.DocumentStore.Options.DatabaseSchemaName}.{UsageTable}";
        var rows = await session.AdvancedSql.QueryAsync<int>(
            $"""
            insert into {table} (organisation_id, limit_name, period_start, used) values (?, ?, ?, ?)
            on conflict (organisation_id, limit_name, period_start)
            do update set used = {UsageTable}.used + excluded.used where {UsageTable}.used + excluded.used <= ?
            returning used
            """,
            cancellationToken,
            organisationId, limit, periodStart.ToUniversalTime(), amount, max);
        return rows.Count > 0;
    }

    public Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        session.Delete<BillingAccount>(organisationId);
        session.QueueSqlCommand($"delete from {session.DocumentStore.Options.DatabaseSchemaName}.{UsageTable} where organisation_id = ?", organisationId);
        return session.SaveChangesAsync(cancellationToken);
    }
}
