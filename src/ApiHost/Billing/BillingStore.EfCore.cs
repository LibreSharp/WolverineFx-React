using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Billing;

// A row of PerPeriod usage: how much of a limit an organisation used in one billing period.
public class PeriodUsage
{
    public Guid OrganisationId { get; set; }

    public string LimitName { get; set; } = string.Empty;

    public DateTimeOffset PeriodStart { get; set; }

    public int Used { get; set; }
}

// Billing accounts and usage are ordinary (not tenanted) tables; see Data/EfCore/AppDbContext.cs.
public class BillingStore(AppDbContext db) : IBillingStore
{
    public Task<BillingAccount?> FindAccountAsync(Guid organisationId, CancellationToken cancellationToken)
        => db.BillingAccounts.FirstOrDefaultAsync(a => a.Id == organisationId, cancellationToken);

    public async Task SaveAccountAsync(BillingAccount account, CancellationToken cancellationToken)
    {
        if (db.Entry(account).State == EntityState.Detached)
        {
            db.BillingAccounts.Add(account);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> UsageAsync(Guid organisationId, string limit, DateTimeOffset periodStart, CancellationToken cancellationToken)
    {
        var start = periodStart.ToUniversalTime();
        return db.PeriodUsage
            .Where(u => u.OrganisationId == organisationId && u.LimitName == limit && u.PeriodStart == start)
            .Select(u => u.Used)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryUseAsync(Guid organisationId, string limit, DateTimeOffset periodStart, int amount, int max, CancellationToken cancellationToken)
    {
        // Inserts the period's row, or adds to it only while the total stays within max: no row is
        // changed when it would go over.
        var start = periodStart.ToUniversalTime();
        var changed = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "PeriodUsage" ("OrganisationId", "LimitName", "PeriodStart", "Used") VALUES ({organisationId}, {limit}, {start}, {amount})
            ON CONFLICT ("OrganisationId", "LimitName", "PeriodStart")
            DO UPDATE SET "Used" = "PeriodUsage"."Used" + EXCLUDED."Used" WHERE "PeriodUsage"."Used" + EXCLUDED."Used" <= {max}
            """,
            cancellationToken);
        return changed > 0;
    }

    public async Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await db.PeriodUsage.Where(u => u.OrganisationId == organisationId).ExecuteDeleteAsync(cancellationToken);
        await db.BillingAccounts.Where(a => a.Id == organisationId).ExecuteDeleteAsync(cancellationToken);
    }
}
