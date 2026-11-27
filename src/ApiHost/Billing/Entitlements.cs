using ApiHost.Organisations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Wolverine.Http;

namespace ApiHost.Billing;

// How much of a limit an organisation is using. Limit: null when the plan has no limit.
public record LimitUsage(string Name, string Kind, string Description, int? Limit, int Used)
{
    // The client warns at 80% and stops at 100%.
    public const int WarningPercent = 80;

    public bool NearLimit => Limit is { } limit && Used * 100 >= limit * WarningPercent;

    public bool Reached => Limit is { } limit && Used >= limit;
}

// The one place that answers "can this organisation use feature X?" and "how much of limit Y is
// left?", from the plan it's on now. Endpoints and handlers check through it (see the Todo
// endpoints' Before methods), and GET /api/billing gives the client the same answers to show or
// hide features and upgrade prompts. Scoped: each organisation's state is worked out once per
// request or message.
public class Entitlements(
    IOrganisationStore organisations,
    IBillingStore store,
    IFeatureManager featureManager,
    IEnumerable<ILimitUsage> usages,
    IOptions<BillingOptions> options,
    TimeProvider clock)
{
    private readonly Dictionary<Guid, BillingState> _states = [];
    private IReadOnlyList<FeatureOverride>? _overrides;

    public async Task<BillingState> StateAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        if (!_states.TryGetValue(organisationId, out var state))
        {
            var now = clock.GetUtcNow();
            // A deleted organisation (a message handled after it went) has no trial.
            var organisation = await organisations.FindAsync(organisationId, cancellationToken)
                ?? new Organisation { Id = organisationId, CreatedAt = now.AddYears(-1) };
            var account = await store.FindAccountAsync(organisationId, cancellationToken);
            state = BillingState.Resolve(organisation, account, options.Value, now);
            _states[organisationId] = state;
        }

        return state;
    }

    // Forgets the cached state, after the organisation's billing account changes.
    public void Refresh(Guid organisationId)
    {
        _states.Remove(organisationId);
        _overrides = null;
    }

    public async Task<bool> IsEnabledAsync(Guid organisationId, string feature, CancellationToken cancellationToken)
    {
        var state = await StateAsync(organisationId, cancellationToken);
        _overrides ??= await store.FeatureOverridesAsync(cancellationToken);
        return await featureManager.IsEnabledAsync(feature, new OrganisationFeatureContext(organisationId, state.Plan, _overrides));
    }

    public async Task<LimitUsage> UsageAsync(Guid organisationId, string limit, CancellationToken cancellationToken)
    {
        var definition = PlanLimits.Get(limit);
        var state = await StateAsync(organisationId, cancellationToken);
        var used = definition.Kind == LimitKinds.PerPeriod
            ? await store.UsageAsync(organisationId, limit, state.PeriodStart, cancellationToken)
            : await CounterFor(limit).CountAsync(organisationId, cancellationToken);
        return new LimitUsage(definition.Name, definition.Kind, definition.Description, state.Plan.LimitOf(limit), used);
    }

    public async Task<IReadOnlyList<LimitUsage>> AllUsageAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var all = new List<LimitUsage>();
        foreach (var limit in PlanLimits.All)
        {
            all.Add(await UsageAsync(organisationId, limit.Name, cancellationToken));
        }

        return all;
    }

    // A Count limit: is there room for this many more? Two requests at the same moment can both
    // take the last place, so a Count limit can be passed by one or two; PerPeriod limits can't.
    public async Task<bool> CanAddAsync(Guid organisationId, string limit, int adding, CancellationToken cancellationToken)
    {
        var usage = await UsageAsync(organisationId, limit, cancellationToken);
        return usage.Limit is not { } max || usage.Used + adding <= max;
    }

    // A PerPeriod limit: uses this much of it if there's room, and says whether it did.
    public async Task<bool> TryUseAsync(Guid organisationId, string limit, int amount, CancellationToken cancellationToken)
    {
        if (PlanLimits.Get(limit).Kind != LimitKinds.PerPeriod)
        {
            throw new ArgumentException($"'{limit}' isn't a PerPeriod limit", nameof(limit));
        }

        var state = await StateAsync(organisationId, cancellationToken);
        if (state.Plan.LimitOf(limit) is not { } max)
        {
            max = int.MaxValue;
        }

        return amount <= max && await store.TryUseAsync(organisationId, limit, state.PeriodStart, amount, max, cancellationToken);
    }

    // For an endpoint's Before method: carry on (WolverineContinue.NoProblems) if the feature is
    // in the organisation's plan, otherwise answer 402 Payment Required.
    public async Task<ProblemDetails> RequireFeatureAsync(Guid organisationId, string feature, CancellationToken cancellationToken)
        => await IsEnabledAsync(organisationId, feature, cancellationToken)
            ? WolverineContinue.NoProblems
            : EntitlementProblems.FeatureNotInPlan(feature, (await StateAsync(organisationId, cancellationToken)).Plan);

    // For an endpoint's Before method: carry on if there's room for this many more of a Count
    // limit, otherwise answer 402 Payment Required.
    public async Task<ProblemDetails> RequireRoomAsync(Guid organisationId, string limit, int adding, CancellationToken cancellationToken)
        => await CanAddAsync(organisationId, limit, adding, cancellationToken)
            ? WolverineContinue.NoProblems
            : await LimitReachedAsync(organisationId, limit, cancellationToken);

    // The 402 Payment Required answer for a limit that has been reached.
    public async Task<ProblemDetails> LimitReachedAsync(Guid organisationId, string limit, CancellationToken cancellationToken)
        => EntitlementProblems.LimitReached(await UsageAsync(organisationId, limit, cancellationToken), (await StateAsync(organisationId, cancellationToken)).Plan);

    private ILimitUsage CounterFor(string limit)
        => usages.FirstOrDefault(u => u.Limit == limit)
            ?? throw new InvalidOperationException($"No ILimitUsage is registered for the Count limit '{limit}' (see BillingSetup)");
}

// 402 Payment Required, with the feature or limit, so the client can offer an upgrade.
public static class EntitlementProblems
{
    public static ProblemDetails FeatureNotInPlan(string feature, Plan plan) => new()
    {
        Status = StatusCodes.Status402PaymentRequired,
        Title = "Upgrade required",
        Detail = $"{PlanFeatures.Descriptions.GetValueOrDefault(feature, feature)} isn't included in the {plan.Name} plan.",
        Extensions = { ["feature"] = feature },
    };

    public static ProblemDetails LimitReached(LimitUsage usage, Plan plan) => new()
    {
        Status = StatusCodes.Status402PaymentRequired,
        Title = "Plan limit reached",
        Detail = $"The {plan.Name} plan includes {usage.Limit} {usage.Description.ToLowerInvariant()}, and this organisation has used {usage.Used}.",
        Extensions = { ["limit"] = usage.Name },
    };
}
