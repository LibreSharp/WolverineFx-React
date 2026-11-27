using ApiHost.Billing;
using ApiHost.Organisations;
using Marten;

namespace ApiHost.Features.Admin;

public class BackOfficeQueries(IQuerySession session) : IBackOfficeQueries
{
    public async Task<(IReadOnlyList<OrganisationListRow> Rows, int Total)> OrganisationsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = session.Query<Organisation>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var total = await query.CountAsync(cancellationToken);
        var organisations = await query.OrderBy(o => o.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ids = organisations.Select(o => o.Id).ToList();
        var members = (await session.Query<Membership>().Where(m => ids.Contains(m.OrganisationId)).ToListAsync(cancellationToken))
            .GroupBy(m => m.OrganisationId)
            .ToDictionary(g => g.Key, g => g.Count());
        return ([.. organisations.Select(o => new OrganisationListRow(o.Id, o.Name, o.CreatedAt, members.GetValueOrDefault(o.Id), o.SuspendedAt))], total);
    }

    public async Task<IReadOnlyList<BillingAccount>> AccountsAsync(IReadOnlyCollection<Guid> organisationIds, CancellationToken cancellationToken)
        => await session.LoadManyAsync<BillingAccount>(cancellationToken, organisationIds);

    public async Task<IReadOnlyList<BillingAccount>> LiveAccountsAsync(CancellationToken cancellationToken)
        => await session.Query<BillingAccount>()
            .Where(a => a.Status == SubscriptionStatuses.Active || a.Status == SubscriptionStatuses.PastDue || a.Status == SubscriptionStatuses.Trialing)
            .ToListAsync(cancellationToken);

    public Task<int> OrganisationsCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
        => session.Query<Organisation>().CountAsync(o => o.CreatedAt >= from && o.CreatedAt < to, cancellationToken);

    public async Task<int> OrganisationsWithoutAccountCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var ids = await session.Query<Organisation>().Where(o => o.CreatedAt >= from && o.CreatedAt < to).Select(o => o.Id).ToListAsync(cancellationToken);
        var withAccount = (await session.LoadManyAsync<BillingAccount>(cancellationToken, ids)).Count;
        return ids.Count - withAccount;
    }

    public async Task<int> OrganisationsUsedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
        => (await session.Query<Membership>().Where(m => m.LastUsedAt >= since).Select(m => m.OrganisationId).ToListAsync(cancellationToken))
            .Distinct()
            .Count();
}
