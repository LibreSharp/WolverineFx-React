using ApiHost.Billing;

namespace ApiHost.Features.Admin;

public record OrganisationListRow(Guid Id, string Name, DateTimeOffset CreatedAt, int Members, DateTimeOffset? SuspendedAt);

// The back office's queries across organisations, which the app's own stores (each about one
// organisation) don't answer. In Marten or EF Core (BackOfficeQueries).
public interface IBackOfficeQueries
{
    // Organisations by name, optionally only those whose name contains search. Page numbers
    // start at 1.
    Task<(IReadOnlyList<OrganisationListRow> Rows, int Total)> OrganisationsAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<BillingAccount>> AccountsAsync(IReadOnlyCollection<Guid> organisationIds, CancellationToken cancellationToken);

    // Accounts that are subscribed, failing to pay, or on a trial an admin gave.
    Task<IReadOnlyList<BillingAccount>> LiveAccountsAsync(CancellationToken cancellationToken);

    Task<int> OrganisationsCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    // Organisations created then that have no billing account: still on the reverse trial they
    // started with, if it hasn't ended.
    Task<int> OrganisationsWithoutAccountCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    // Organisations someone started working in since then.
    Task<int> OrganisationsUsedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);
}
