using ApiHost.Billing;
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Features.Admin;

public class BackOfficeQueries(AppDbContext db) : IBackOfficeQueries
{
    public async Task<(IReadOnlyList<OrganisationListRow> Rows, int Total)> OrganisationsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Organisations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            query = query.Where(o => EF.Functions.ILike(o.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(o => o.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrganisationListRow(o.Id, o.Name, o.CreatedAt, db.Memberships.Count(m => m.OrganisationId == o.Id), o.SuspendedAt))
            .ToListAsync(cancellationToken);
        return (rows, total);
    }

    public async Task<IReadOnlyList<BillingAccount>> AccountsAsync(IReadOnlyCollection<Guid> organisationIds, CancellationToken cancellationToken)
        => await db.BillingAccounts.AsNoTracking().Where(a => organisationIds.Contains(a.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BillingAccount>> LiveAccountsAsync(CancellationToken cancellationToken)
        => await db.BillingAccounts.AsNoTracking()
            .Where(a => a.Status == SubscriptionStatuses.Active || a.Status == SubscriptionStatuses.PastDue || a.Status == SubscriptionStatuses.Trialing)
            .ToListAsync(cancellationToken);

    public Task<int> OrganisationsCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        => db.Organisations.CountAsync(o => o.CreatedAt >= from && o.CreatedAt < to, cancellationToken);

    public Task<int> OrganisationsWithoutAccountCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        => db.Organisations.CountAsync(o => o.CreatedAt >= from && o.CreatedAt < to && !db.BillingAccounts.Any(a => a.Id == o.Id), cancellationToken);

    public Task<int> OrganisationsUsedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
        => db.Memberships.Where(m => m.LastUsedAt >= since).Select(m => m.OrganisationId).Distinct().CountAsync(cancellationToken);
}
