using ApiHost.Organisations;

namespace ApiHost.Billing;

public static class BillingStatuses
{
    public const string Trial = "Trial";
    public const string Active = "Active";
    public const string PastDue = "PastDue";
    public const string Free = "Free";
}

// The plan an organisation is on now, worked out from its BillingAccount (if any) and the time.
// The billing period is the paid subscription's; otherwise a month at a time from the day the
// organisation was created. PerPeriod limits start again each period.
public record BillingState(
    Plan Plan,
    string Status,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    bool CancelAtPeriodEnd)
{
    public static BillingState Resolve(Organisation organisation, BillingAccount? account, BillingOptions options, DateTimeOffset now)
    {
        var (monthStart, monthEnd) = MonthContaining(organisation.CreatedAt, now);

        if (account?.Status is SubscriptionStatuses.Active or SubscriptionStatuses.PastDue)
        {
            // An old version that has since been removed from the catalogue: its latest.
            var plan = PlanCatalogue.Find(account.PlanId, account.PlanVersion) ?? PlanCatalogue.Latest(account.PlanId);
            return new BillingState(
                plan,
                account.Status == SubscriptionStatuses.Active ? BillingStatuses.Active : BillingStatuses.PastDue,
                null,
                account.CurrentPeriodStart ?? monthStart,
                account.CurrentPeriodEnd ?? monthEnd,
                account.CancelAtPeriodEnd);
        }

        // No account yet: the reverse trial that every new organisation starts with.
        var (trialPlan, trialEndsAt) = account is null
            ? (options.TrialPlan, options.TrialDays > 0 ? organisation.CreatedAt.AddDays(options.TrialDays) : (DateTimeOffset?)null)
            : (account.PlanId, account.Status == SubscriptionStatuses.Trialing ? account.TrialEndsAt : null);
        if (trialEndsAt > now)
        {
            return new BillingState(PlanCatalogue.Latest(trialPlan), BillingStatuses.Trial, trialEndsAt, monthStart, monthEnd, false);
        }

        return new BillingState(PlanCatalogue.Latest(PlanCatalogue.Free), BillingStatuses.Free, null, monthStart, monthEnd, false);
    }

    // The month (from the anchor's day of the month) that contains now.
    public static (DateTimeOffset Start, DateTimeOffset End) MonthContaining(DateTimeOffset anchor, DateTimeOffset now)
    {
        anchor = anchor.ToUniversalTime();
        var months = ((now.Year - anchor.Year) * 12) + now.Month - anchor.Month;
        if (anchor.AddMonths(months) > now)
        {
            months--;
        }

        // From the anchor each time, so the 31st stays the 31st after a shorter month.
        return (anchor.AddMonths(Math.Max(months, 0)), anchor.AddMonths(Math.Max(months, 0) + 1));
    }
}
