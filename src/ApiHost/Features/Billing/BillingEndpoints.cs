using System.Security.Claims;
using ApiHost.Billing;
using ApiHost.Billing.Payments;
using ApiHost.Identity;
using ApiHost.Organisations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Wolverine.Http;

namespace ApiHost.Features.Billing;

// The current organisation's plan: what it includes, how much of each limit is used, and whether
// the signed-in user can change it (owners only). The client uses it to show or hide features and
// upgrade prompts. PaymentsEnabled: paid plans can be bought (a payment provider is set up).
// HasCustomer: the organisation has subscribed before, so the provider's portal is available.
public record BillingSummary(
    string PlanId,
    int PlanVersion,
    string PlanName,
    string Status,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    bool CancelAtPeriodEnd,
    IReadOnlyList<string> Features,
    IReadOnlyList<LimitUsage> Limits,
    bool CanManage,
    bool PaymentsEnabled,
    bool HasCustomer);

public record PlanFeatureOffer(string Name, string Description, bool Included);

public record PlanLimitOffer(string Name, string Description, int? Limit);

public record PlanOffer(
    string Id,
    int Version,
    string Name,
    string Description,
    bool IsFree,
    IReadOnlyList<PlanPrice> Prices,
    IReadOnlyList<PlanFeatureOffer> Features,
    IReadOnlyList<PlanLimitOffer> Limits);

// TrialDays and TrialPlanId: the reverse trial a new organisation starts with.
public record PlanCatalogueResponse(IReadOnlyList<PlanOffer> Plans, int TrialDays, string TrialPlanId, string Currency);

public static class BillingEndpoints
{
    [Authorize]
    [WolverineGet("/api/billing")]
    public static async Task<BillingSummary> Get(
        ClaimsPrincipal user,
        Entitlements entitlements,
        IOrganisationStore organisations,
        IBillingStore store,
        IPaymentProvider provider,
        CancellationToken cancellationToken)
    {
        var organisationId = user.GetOrganisationId();
        var state = await entitlements.StateAsync(organisationId, cancellationToken);
        var features = new List<string>();
        foreach (var feature in PlanFeatures.Descriptions.Keys)
        {
            if (await entitlements.IsEnabledAsync(organisationId, feature, cancellationToken))
            {
                features.Add(feature);
            }
        }

        var membership = await organisations.FindMembershipAsync(organisationId, user.GetUserId(), cancellationToken);
        return new BillingSummary(
            state.Plan.Id,
            state.Plan.Version,
            state.Plan.Name,
            state.Status,
            state.TrialEndsAt,
            state.PeriodStart,
            state.PeriodEnd,
            state.CancelAtPeriodEnd,
            features,
            await entitlements.AllUsageAsync(organisationId, cancellationToken),
            membership?.Role == OrganisationRoles.Owner,
            provider.IsConfigured,
            (await store.FindAccountAsync(organisationId, cancellationToken))?.CustomerId is not null);
    }

    // The plans on offer, for the pricing page. Public: the pricing page is for visitors. The
    // pricing page is pre-rendered from a copy (src/Client/src/features/billing/plans.json), which
    // a test keeps the same as this (docs/billing.md).
    [AllowAnonymous]
    [WolverineGet("/api/billing/plans")]
    public static PlanCatalogueResponse Plans(IOptions<BillingOptions> options)
        => new(
            [.. PlanCatalogue.Offered.Select(plan => new PlanOffer(
                plan.Id,
                plan.Version,
                plan.Name,
                plan.Description,
                plan.IsFree,
                plan.Prices,
                [.. PlanFeatures.Descriptions.Select(f => new PlanFeatureOffer(f.Key, f.Value, plan.Includes(f.Key)))],
                [.. PlanLimits.All.Select(l => new PlanLimitOffer(l.Name, l.Description, plan.LimitOf(l.Name)))]))],
            options.Value.TrialDays,
            options.Value.TrialPlan,
            options.Value.Currency);
}
