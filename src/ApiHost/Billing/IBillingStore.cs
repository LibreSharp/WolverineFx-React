using ApiHost.Billing.Payments;

namespace ApiHost.Billing;

// Billing accounts and PerPeriod usage, in Marten or EF Core (BillingStore). Each change is saved
// straight away.
public interface IBillingStore
{
    Task<BillingAccount?> FindAccountAsync(Guid organisationId, CancellationToken cancellationToken);

    // Saves the account, and adds or updates the invoice with it, in one save (a handler's session
    // can only be saved once).
    Task SaveAccountAsync(BillingAccount account, CancellationToken cancellationToken, BillingInvoice? invoice = null);

    // The account with the payment provider's customer id.
    Task<BillingAccount?> FindAccountByCustomerAsync(string customerId, CancellationToken cancellationToken);

    // Records a webhook event and publishes PaymentEventReceived for it, in one transaction
    // (Wolverine's outbox), so it's handled exactly once even if the provider delivers it again.
    // False, and nothing published, if it was received before.
    Task<bool> RecordPaymentEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken);

    // The organisation's invoices, newest first.
    Task<IReadOnlyList<BillingInvoice>> InvoicesAsync(Guid organisationId, CancellationToken cancellationToken);

    // How much of a PerPeriod limit the organisation has used in the period starting then.
    Task<int> UsageAsync(Guid organisationId, string limit, DateTimeOffset periodStart, CancellationToken cancellationToken);

    // Adds amount to that usage if the total stays within max, and says whether it did. One
    // database statement, so requests at the same moment can't take the total past max.
    Task<bool> TryUseAsync(Guid organisationId, string limit, DateTimeOffset periodStart, int amount, int max, CancellationToken cancellationToken);

    // A deleted organisation's account, usage and invoices.
    Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken);
}

// How much of a Count limit (LimitKinds.Count) an organisation is using now, such as its open
// Todos. The feature that owns the data counts it; register one for each Count limit (BillingSetup).
public interface ILimitUsage
{
    string Limit { get; }

    Task<int> CountAsync(Guid organisationId, CancellationToken cancellationToken);
}
