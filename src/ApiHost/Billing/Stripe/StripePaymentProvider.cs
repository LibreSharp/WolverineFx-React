using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ApiHost.Billing.Payments;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace ApiHost.Billing.Stripe;

// Set in the "Billing:Stripe" section, e.g. Billing__Stripe__SecretKey. Both keys are secrets:
// user secrets in development, environment variables in production.
public class StripeOptions
{
    public const string SectionName = "Billing:Stripe";

    // From the Stripe dashboard (Developers > API keys): sk_test_... while testing, sk_live_... in
    // production.
    public string? SecretKey { get; set; }

    // The webhook endpoint's signing secret (whsec_...), from the dashboard or `stripe listen`.
    public string? WebhookSecret { get; set; }

    // Stripe Tax: Stripe works out and adds sales tax and VAT. Turn it on in the Stripe dashboard
    // first. (Stripe charges for it.)
    public bool AutomaticTax { get; set; }
}

// Stripe Checkout for new subscriptions, the customer portal for changes, and webhooks for
// everything that happens afterwards. Plans are found by their prices' lookup keys
// ("pro-v1-usd"), which `dotnet run -- stripe-prices` creates from the plan catalogue.
public partial class StripePaymentProvider(IOptions<StripeOptions> options, TimeProvider clock) : IPaymentProvider
{
    public const string ProviderName = "stripe";
    private const string OrganisationKey = "organisation_id";

    private StripeClient? _client;

    public string Name => ProviderName;

    public bool IsConfigured => !string.IsNullOrEmpty(options.Value.SecretKey) && !string.IsNullOrEmpty(options.Value.WebhookSecret);

    private StripeClient Client => _client ??= new StripeClient(options.Value.SecretKey
        ?? throw new InvalidOperationException($"Set {StripeOptions.SectionName}:SecretKey (see docs/billing.md)"));

    public static string LookupKey(Plan plan, string currency)
        => $"{plan.Id}-v{plan.Version}-{currency.ToLowerInvariant()}";

    // The plan and version a price's lookup key stands for.
    public static (string PlanId, int Version)? PlanOf(string? lookupKey)
        => lookupKey is not null && LookupKeyPattern().Match(lookupKey) is { Success: true } match
            ? (match.Groups["plan"].Value, int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture))
            : null;

    [GeneratedRegex(@"^(?<plan>[a-z0-9_-]+)-v(?<version>\d+)-[a-z]{3}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LookupKeyPattern();

    public async Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var key = LookupKey(request.Plan, request.Currency);
        var prices = await new PriceService(Client).ListAsync(new PriceListOptions { LookupKeys = [key], Active = true }, cancellationToken: cancellationToken);
        var price = prices.Data.FirstOrDefault()
            ?? throw new InvalidOperationException($"Stripe has no active price with the lookup key '{key}': run `dotnet run -- stripe-prices` (docs/billing.md)");

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { [OrganisationKey] = request.OrganisationId.ToString() };
        var session = await new SessionService(Client).CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = [new SessionLineItemOptions { Price = price.Id, Quantity = 1 }],
            ClientReferenceId = request.OrganisationId.ToString(),
            Customer = request.CustomerId,
            CustomerEmail = request.CustomerId is null ? request.Email : null,
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = metadata,
                // What's left of the trial carries on; Stripe needs it to end at least two days ahead.
                TrialEnd = request.TrialEndsAt > clock.GetUtcNow().AddDays(2) ? request.TrialEndsAt.Value.UtcDateTime : null,
            },
            AutomaticTax = options.Value.AutomaticTax ? new SessionAutomaticTaxOptions { Enabled = true } : null,
            AllowPromotionCodes = true,
            SuccessUrl = request.SuccessUrl.ToString(),
            CancelUrl = request.CancelUrl.ToString(),
        }, cancellationToken: cancellationToken);
        return new Uri(session.Url);
    }

    public async Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken)
    {
        var session = await new global::Stripe.BillingPortal.SessionService(Client).CreateAsync(
            new global::Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl.ToString() },
            cancellationToken: cancellationToken);
        return new Uri(session.Url);
    }

    public async Task<ProviderSubscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        Subscription subscription;
        try
        {
            subscription = await new SubscriptionService(Client).GetAsync(subscriptionId, cancellationToken: cancellationToken);
        }
        catch (StripeException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var item = subscription.Items.Data.FirstOrDefault()
            ?? throw new InvalidOperationException($"Stripe subscription {subscription.Id} has no items");

        // A price that wasn't created from the plan catalogue: the message fails and waits in the
        // dead-letter queue (the back office's failed messages) until the price is fixed.
        var (planId, version) = PlanOf(item.Price.LookupKey)
            ?? throw new InvalidOperationException($"Stripe price {item.Price.Id} has no plan lookup key (such as 'pro-v1-usd'); create prices with `dotnet run -- stripe-prices`");

        return new ProviderSubscription(
            subscription.Id,
            subscription.CustomerId,
            OrganisationIdIn(subscription.Metadata),
            planId,
            version,
            StatusOf(subscription.Status),
            subscription.Currency.ToUpperInvariant(),
            new DateTimeOffset(item.CurrentPeriodStart, TimeSpan.Zero),
            new DateTimeOffset(item.CurrentPeriodEnd, TimeSpan.Zero),
            subscription.CancelAtPeriodEnd || subscription.CancelAt is not null);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        try
        {
            await new SubscriptionService(Client).CancelAsync(subscriptionId, cancellationToken: cancellationToken);
        }
        catch (StripeException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Already gone.
        }
    }

    public PaymentEvent? ReadWebhook(string payload, IHeaderDictionary headers)
    {
        if (!IsConfigured)
        {
            return null;
        }

        Event stripeEvent;
        try
        {
            // Checks the Stripe-Signature header against the webhook secret, and that the event is
            // recent (five minutes), so a captured request can't be replayed later.
            stripeEvent = EventUtility.ConstructEvent(payload, headers["Stripe-Signature"].ToString(), options.Value.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return null;
        }

        return stripeEvent.Data.Object switch
        {
            Session { Mode: "subscription" } session => new PaymentEvent(
                Name, stripeEvent.Id, stripeEvent.Type,
                Guid.TryParse(session.ClientReferenceId, out var organisationId) ? organisationId : OrganisationIdIn(session.Metadata),
                session.CustomerId, session.SubscriptionId, null),
            Subscription subscription => new PaymentEvent(
                Name, stripeEvent.Id, stripeEvent.Type, OrganisationIdIn(subscription.Metadata), subscription.CustomerId, subscription.Id, null),
            Invoice invoice => new PaymentEvent(
                Name, stripeEvent.Id, stripeEvent.Type, null, invoice.CustomerId, invoice.Parent?.SubscriptionDetails?.SubscriptionId, InvoiceOf(invoice)),
            _ => new PaymentEvent(Name, stripeEvent.Id, stripeEvent.Type, null, null, null, null),
        };
    }

    public Uri? DashboardLink(string kind, string id)
    {
        var mode = options.Value.SecretKey?.StartsWith("sk_test_", StringComparison.Ordinal) == true ? "test/" : string.Empty;
        return kind switch
        {
            "customer" => new Uri($"https://dashboard.stripe.com/{mode}customers/{Uri.EscapeDataString(id)}"),
            "subscription" => new Uri($"https://dashboard.stripe.com/{mode}subscriptions/{Uri.EscapeDataString(id)}"),
            "invoice" => new Uri($"https://dashboard.stripe.com/{mode}invoices/{Uri.EscapeDataString(id)}"),
            _ => null,
        };
    }

    // Stripe's subscription statuses, in the app's terms.
    public static string StatusOf(string stripeStatus) => stripeStatus switch
    {
        // A trial with payment details taken: the plan applies, and Stripe charges when it ends.
        "active" or "trialing" => SubscriptionStatuses.Active,
        "past_due" or "unpaid" => SubscriptionStatuses.PastDue,
        "canceled" or "incomplete_expired" => SubscriptionStatuses.Canceled,
        // "incomplete" (the first payment hasn't gone through) and "paused": no plan yet.
        _ => SubscriptionStatuses.None,
    };

    private static ProviderInvoice InvoiceOf(Invoice invoice) => new(
        invoice.Id,
        invoice.CustomerId,
        invoice.Parent?.SubscriptionDetails?.SubscriptionId,
        invoice.Number,
        invoice.Status,
        invoice.Total,
        invoice.AmountPaid,
        invoice.Currency.ToUpperInvariant(),
        new DateTimeOffset(invoice.Created, TimeSpan.Zero),
        new DateTimeOffset(invoice.PeriodStart, TimeSpan.Zero),
        new DateTimeOffset(invoice.PeriodEnd, TimeSpan.Zero),
        invoice.HostedInvoiceUrl,
        invoice.InvoicePdf);

    private static Guid? OrganisationIdIn(Dictionary<string, string>? metadata)
        => metadata?.TryGetValue(OrganisationKey, out var value) == true && Guid.TryParse(value, out var id) ? id : null;
}
