namespace ApiHost.Billing;

// Billing accounts and PerPeriod usage, in Marten or EF Core (BillingStore). Each change is saved
// straight away.
public interface IBillingStore
{
    Task<BillingAccount?> FindAccountAsync(Guid organisationId, CancellationToken cancellationToken);

    Task SaveAccountAsync(BillingAccount account, CancellationToken cancellationToken);

    // How much of a PerPeriod limit the organisation has used in the period starting then.
    Task<int> UsageAsync(Guid organisationId, string limit, DateTimeOffset periodStart, CancellationToken cancellationToken);

    // Adds amount to that usage if the total stays within max, and says whether it did. One
    // database statement, so requests at the same moment can't take the total past max.
    Task<bool> TryUseAsync(Guid organisationId, string limit, DateTimeOffset periodStart, int amount, int max, CancellationToken cancellationToken);

    // A deleted organisation's account and usage.
    Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken);
}

// How much of a Count limit (LimitKinds.Count) an organisation is using now, such as its open
// Todos. The feature that owns the data counts it; register one for each Count limit (BillingSetup).
public interface ILimitUsage
{
    string Limit { get; }

    Task<int> CountAsync(Guid organisationId, CancellationToken cancellationToken);
}
