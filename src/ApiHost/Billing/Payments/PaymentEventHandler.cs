using ApiHost.Organisations;

namespace ApiHost.Billing.Payments;

// A webhook event, stored and sent on a durable local queue by the webhook endpoint (through the
// outbox), so a crash or a provider outage only delays it: Wolverine retries it, and parks it in
// the dead-letter queue if it keeps failing.
public record PaymentEventReceived(PaymentEvent Event);

// Keeps the organisation's BillingAccount (the app's copy of its billing state) in step with the
// provider. It reads the subscription again rather than trusting the event's copy, so events that
// arrive late or out of order can't leave an old state behind.
public static class PaymentEventHandler
{
    public static async Task Handle(
        PaymentEventReceived message,
        IPaymentProvider provider,
        IBillingStore store,
        IOrganisationStore organisations,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var paymentEvent = message.Event;
        var subscription = paymentEvent.SubscriptionId is { } subscriptionId
            ? await provider.GetSubscriptionAsync(subscriptionId, cancellationToken)
            : null;

        var account = await FindAccountAsync(paymentEvent, subscription, store, organisations, cancellationToken);
        if (account is null)
        {
            // Not one of this app's organisations (or one that has since been deleted).
            return;
        }

        if (subscription is not null && Applies(account, subscription))
        {
            account.Provider = provider.Name;
            account.CustomerId = subscription.CustomerId;
            account.SubscriptionId = subscription.Id;
            account.PlanId = subscription.PlanId;
            account.PlanVersion = subscription.PlanVersion;
            account.Status = subscription.Status;
            account.Currency = subscription.Currency;
            account.CurrentPeriodStart = subscription.PeriodStart;
            account.CurrentPeriodEnd = subscription.PeriodEnd;
            account.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;
        }
        else if (paymentEvent.CustomerId is not null && account.CustomerId is null)
        {
            account.Provider = provider.Name;
            account.CustomerId = paymentEvent.CustomerId;
        }

        account.UpdatedAt = clock.GetUtcNow();
        var invoice = paymentEvent.Invoice is not { } received ? null : new BillingInvoice
        {
            Id = received.Id,
            OrganisationId = account.Id,
            Provider = provider.Name,
            Number = received.Number,
            Status = received.Status,
            Total = received.Total,
            AmountPaid = received.AmountPaid,
            Currency = received.Currency,
            CreatedAt = received.CreatedAt,
            PeriodStart = received.PeriodStart,
            PeriodEnd = received.PeriodEnd,
            HostedUrl = received.HostedUrl,
            PdfUrl = received.PdfUrl,
        };
        await store.SaveAccountAsync(account, cancellationToken, invoice);
    }

    // The organisation from the event or the subscription's metadata (set at checkout), else the
    // account that has the customer.
    private static async Task<BillingAccount?> FindAccountAsync(
        PaymentEvent paymentEvent, ProviderSubscription? subscription, IBillingStore store, IOrganisationStore organisations, CancellationToken cancellationToken)
    {
        if ((paymentEvent.OrganisationId ?? subscription?.OrganisationId) is { } organisationId)
        {
            if (await organisations.FindAsync(organisationId, cancellationToken) is null)
            {
                return null;
            }

            return await store.FindAccountAsync(organisationId, cancellationToken) ?? new BillingAccount { Id = organisationId };
        }

        var customerId = paymentEvent.CustomerId ?? subscription?.CustomerId;
        return customerId is null ? null : await store.FindAccountByCustomerAsync(customerId, cancellationToken);
    }

    // A subscription other than the account's current one only replaces it if it's live: a late
    // "cancelled" for an old subscription mustn't end the one that replaced it.
    private static bool Applies(BillingAccount account, ProviderSubscription subscription)
        => account.SubscriptionId is null
           || account.SubscriptionId == subscription.Id
           || subscription.Status is SubscriptionStatuses.Active or SubscriptionStatuses.PastDue;
}
