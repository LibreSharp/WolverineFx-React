namespace ApiHost.Billing;

// The app's own copy of an organisation's billing state, kept in step by the payment provider's
// webhooks, so that "which plan is this organisation on?" never calls the provider. Not tenanted:
// one per organisation, with the organisation's id.
//
// An organisation has none until it subscribes (or an admin changes its trial): until then it's
// on the reverse trial and then the free plan (BillingState.Resolve).
public class BillingAccount
{
    public Guid Id { get; set; }

    public string PlanId { get; set; } = PlanCatalogue.Free;

    // The version subscribed to, which the organisation keeps when the plan changes.
    public int PlanVersion { get; set; } = 1;

    public string Status { get; set; } = SubscriptionStatuses.None;

    public DateTimeOffset? TrialEndsAt { get; set; }

    // The paid subscription's current billing period, from the provider.
    public DateTimeOffset? CurrentPeriodStart { get; set; }

    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    // The subscription has been cancelled and ends with the current period.
    public bool CancelAtPeriodEnd { get; set; }

    public string? Currency { get; set; }

    // The payment provider ("stripe") and its ids for the organisation and its subscription.
    public string? Provider { get; set; }

    public string? CustomerId { get; set; }

    public string? SubscriptionId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

// The app's copy of an invoice from the payment provider, for the back office. The provider keeps
// the real one: customers see theirs in its portal. Id: the provider's.
public class BillingInvoice
{
    public string Id { get; set; } = string.Empty;

    public Guid OrganisationId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? Number { get; set; }

    // The provider's: "paid", "open", "void", "uncollectible" or "draft".
    public string Status { get; set; } = string.Empty;

    // In the currency's smallest unit.
    public long Total { get; set; }

    public long AmountPaid { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }

    public string? HostedUrl { get; set; }

    public string? PdfUrl { get; set; }
}

// A webhook event the app has received, so a second delivery of it is ignored. Id:
// "{provider}:{event id}".
public class PaymentEventRecord
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }
}

public static class SubscriptionStatuses
{
    // No subscription: the free plan, once any trial is over.
    public const string None = "None";
    public const string Trialing = "Trialing";
    public const string Active = "Active";

    // A payment failed. The plan carries on while the provider retries; if every retry fails,
    // the provider cancels the subscription.
    public const string PastDue = "PastDue";
    public const string Canceled = "Canceled";
}

// Set in the "Billing" section, e.g. Billing__TrialDays.
public class BillingOptions
{
    public const string SectionName = "Billing";

    // The reverse trial: a new organisation gets TrialPlan for this many days, then the free plan
    // unless it subscribes. 0 turns trials off.
    public int TrialDays { get; set; } = 14;

    public string TrialPlan { get; set; } = PlanCatalogue.Pro;

    // The currency of prices shown and charged, when the organisation hasn't chosen one.
    public string Currency { get; set; } = "USD";
}
