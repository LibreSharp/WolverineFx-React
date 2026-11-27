namespace ApiHost.Billing.Payments;

// A payment provider: takes payment, manages subscriptions, and reports changes by webhook. The
// app uses Stripe (Billing/Stripe/); another provider (Paddle or Lemon Squeezy, which handle sales
// tax and VAT as the seller) implements this interface and is registered in BillingSetup instead.
// Everything else (the plans, the local copy of billing state, limits) stays the same.
public interface IPaymentProvider
{
    // Stored on billing accounts: "stripe".
    string Name { get; }

    // False until it has its keys; the app then offers no paid plans.
    bool IsConfigured { get; }

    // A page on the provider's site where the user pays for the plan; the provider sends them
    // back to SuccessUrl or CancelUrl.
    Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);

    // A page on the provider's site where the customer changes plan or payment details, sees
    // invoices, or cancels.
    Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken);

    // The subscription as the provider has it now, or null if it doesn't exist.
    Task<ProviderSubscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    // Ends the subscription now, with no refund (a deleted organisation's).
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);

    // A webhook request: checks that it really comes from the provider (its signature) and says
    // what happened, or returns null when the signature is wrong.
    PaymentEvent? ReadWebhook(string payload, IHeaderDictionary headers);

    // A link to the object on the provider's dashboard, for the back office.
    Uri? DashboardLink(string kind, string id);
}

// TrialEndsAt: the organisation's trial, which the subscription keeps (it's charged from then).
public record CheckoutRequest(
    Guid OrganisationId,
    Plan Plan,
    string Currency,
    string? CustomerId,
    string? Email,
    DateTimeOffset? TrialEndsAt,
    Uri SuccessUrl,
    Uri CancelUrl);

// Status: one of SubscriptionStatuses. OrganisationId: from the subscription's metadata.
public record ProviderSubscription(
    string Id,
    string CustomerId,
    Guid? OrganisationId,
    string PlanId,
    int PlanVersion,
    string Status,
    string Currency,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    bool CancelAtPeriodEnd);

public record ProviderInvoice(
    string Id,
    string CustomerId,
    string? SubscriptionId,
    string? Number,
    string Status,
    long Total,
    long AmountPaid,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    string? HostedUrl,
    string? PdfUrl);

// What a webhook said happened, reduced to what the app needs: which organisation or customer,
// and the subscription to read again or the invoice to keep. Events the app doesn't use have
// neither. Id: the provider's event id, to ignore deliveries of the same event.
public record PaymentEvent(
    string Provider,
    string Id,
    string Type,
    Guid? OrganisationId,
    string? CustomerId,
    string? SubscriptionId,
    ProviderInvoice? Invoice);
