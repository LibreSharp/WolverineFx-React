using ApiHost.Billing.Payments;
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace ApiHost.Billing;

// A row of PerPeriod usage: how much of a limit an organisation used in one billing period.
public class PeriodUsage
{
    public Guid OrganisationId { get; set; }

    public string LimitName { get; set; } = string.Empty;

    public DateTimeOffset PeriodStart { get; set; }

    public int Used { get; set; }
}

// Billing accounts, usage, invoices and webhook events are ordinary (not tenanted) tables; see
// Data/EfCore/AppDbContext.cs.
public class BillingStore(AppDbContext db, IDbContextOutbox outbox, TimeProvider clock) : IBillingStore
{
    public Task<BillingAccount?> FindAccountAsync(Guid organisationId, CancellationToken cancellationToken)
        => db.BillingAccounts.FirstOrDefaultAsync(a => a.Id == organisationId, cancellationToken);

    public async Task SaveAccountAsync(BillingAccount account, CancellationToken cancellationToken, BillingInvoice? invoice = null)
    {
        if (db.Entry(account).State == EntityState.Detached)
        {
            db.BillingAccounts.Add(account);
        }

        if (invoice is not null)
        {
            var existing = await db.BillingInvoices.FirstOrDefaultAsync(i => i.Id == invoice.Id, cancellationToken);
            if (existing is null)
            {
                db.BillingInvoices.Add(invoice);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(invoice);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<BillingAccount?> FindAccountByCustomerAsync(string customerId, CancellationToken cancellationToken)
        => db.BillingAccounts.FirstOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);

    public async Task<bool> RecordPaymentEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken)
    {
        var id = $"{paymentEvent.Provider}:{paymentEvent.Id}";
        if (await db.PaymentEvents.AnyAsync(e => e.Id == id, cancellationToken))
        {
            return false;
        }

        // The primary key refuses a second delivery that got here at the same moment.
        db.PaymentEvents.Add(new PaymentEventRecord { Id = id, Type = paymentEvent.Type, ReceivedAt = clock.GetUtcNow() });
        outbox.Enroll(db);
        await outbox.PublishAsync(new PaymentEventReceived(paymentEvent));
        try
        {
            await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<BillingInvoice>> InvoicesAsync(Guid organisationId, CancellationToken cancellationToken)
        => await db.BillingInvoices.AsNoTracking()
            .Where(i => i.OrganisationId == organisationId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureOverride>> FeatureOverridesAsync(CancellationToken cancellationToken)
        => await db.FeatureOverrides.AsNoTracking().ToListAsync(cancellationToken);

    public async Task SetFeatureOverrideAsync(string id, FeatureOverride? featureOverride, CancellationToken cancellationToken)
    {
        var existing = await db.FeatureOverrides.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (existing is not null)
        {
            db.FeatureOverrides.Remove(existing);
        }

        if (featureOverride is not null)
        {
            db.FeatureOverrides.Add(featureOverride);
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
        await db.BillingInvoices.Where(i => i.OrganisationId == organisationId).ExecuteDeleteAsync(cancellationToken);
        var key = organisationId.ToString();
        await db.FeatureOverrides.Where(o => o.Scope == FeatureScopes.Organisation && o.ScopeKey == key).ExecuteDeleteAsync(cancellationToken);
        await db.BillingAccounts.Where(a => a.Id == organisationId).ExecuteDeleteAsync(cancellationToken);
    }
}
