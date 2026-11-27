using ApiHost.Billing;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Wolverine.Http;
using Wolverine.Persistence.Durability.DeadLetterManagement;
using Wolverine.Runtime;

namespace ApiHost.Features.Admin;

// Monthly recurring revenue in one currency, in its smallest unit (cents).
public record Revenue(string Currency, long MonthlyAmount);

// The back office's front page. Active: someone started working in it in the last 30 days.
// TrialsEnding: trials (the reverse trial, or one an admin gave) ending in the next 7 days.
// FailedPayments: subscriptions whose payment failed (PastDue).
public record AdminDashboard(
    int Organisations,
    int NewOrganisations,
    int ActiveOrganisations,
    int PayingOrganisations,
    IReadOnlyList<Revenue> MonthlyRevenue,
    int TrialsEnding,
    int FailedPayments,
    int FailedMessages);

public static class AdminDashboardEndpoint
{
    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/dashboard")]
    public static async Task<AdminDashboard> Get(
        IBackOfficeQueries queries, IOptions<BillingOptions> options, IWolverineRuntime runtime, TimeProvider clock, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var monthAgo = now.AddDays(-30);
        var live = await queries.LiveAccountsAsync(cancellationToken);
        var paying = live.Where(a => a.Status is SubscriptionStatuses.Active or SubscriptionStatuses.PastDue).ToList();

        // Revenue from what each subscription's plan version costs in its currency.
        var revenue = paying
            .Select(a => (Plan: PlanCatalogue.Find(a.PlanId, a.PlanVersion) ?? PlanCatalogue.Latest(a.PlanId), Currency: a.Currency ?? options.Value.Currency))
            .Select(x => x.Plan.PriceIn(x.Currency))
            .OfType<PlanPrice>()
            .GroupBy(p => p.Currency.ToUpperInvariant(), StringComparer.Ordinal)
            .Select(g => new Revenue(g.Key, g.Sum(p => p.MonthlyAmount)))
            .OrderBy(r => r.Currency, StringComparer.Ordinal)
            .ToList();

        // Reverse trials end TrialDays after the organisation was created.
        var trialsEnding = live.Count(a => a.Status == SubscriptionStatuses.Trialing && a.TrialEndsAt > now && a.TrialEndsAt <= now.AddDays(7));
        if (options.Value.TrialDays > 0)
        {
            trialsEnding += await queries.OrganisationsWithoutAccountCreatedAsync(
                now.AddDays(-options.Value.TrialDays), now.AddDays(7 - options.Value.TrialDays), cancellationToken);
        }

        var failedMessages = await runtime.Storage.DeadLetters.QueryAsync(
            new DeadLetterEnvelopeQuery { PageNumber = 1, PageSize = 1, Archived = DeadLetterArchiveFilter.Exclude }, cancellationToken);

        return new AdminDashboard(
            await queries.OrganisationsCreatedAsync(DateTimeOffset.MinValue, now.AddMinutes(1), cancellationToken),
            await queries.OrganisationsCreatedAsync(monthAgo, now.AddMinutes(1), cancellationToken),
            await queries.OrganisationsUsedSinceAsync(monthAgo, cancellationToken),
            paying.Count,
            revenue,
            trialsEnding,
            paying.Count(a => a.Status == SubscriptionStatuses.PastDue),
            failedMessages.TotalCount);
    }
}
