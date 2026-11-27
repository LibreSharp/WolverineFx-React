using System.Security.Claims;
using ApiHost.Billing;
using ApiHost.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

namespace ApiHost.Features.Admin;

// A plan's view of a feature: whether the catalogue includes it, and any override.
public record AdminPlanFeature(string PlanId, string PlanName, bool Included, bool? Override);

public record AdminFeature(string Name, string Description, bool? Global, IReadOnlyList<AdminPlanFeature> Plans, int OrganisationOverrides);

// Enabled: on or off; null removes the override, back to what the plans say.
public record SetFeatureRequest(string Scope, string? ScopeKey, bool? Enabled);

public class SetFeatureRequestValidator : AbstractValidator<SetFeatureRequest>
{
    public SetFeatureRequestValidator()
    {
        RuleFor(x => x.Scope).Must(scope => FeatureScopes.All.Contains(scope, StringComparer.Ordinal))
            .WithMessage($"Scope: {string.Join(", ", FeatureScopes.All)}.");
        RuleFor(x => x.ScopeKey).Must(key => PlanCatalogue.All.Any(p => p.Id == key)).When(x => x.Scope == FeatureScopes.Plan)
            .WithMessage("Unknown plan.");
        RuleFor(x => x.ScopeKey).Must(key => Guid.TryParse(key, out _)).When(x => x.Scope == FeatureScopes.Organisation)
            .WithMessage("An organisation's id.");
    }
}

// The back office's feature flags: turn a feature (PlanFeatures) on or off for everyone, for a
// plan, or for one organisation, over what the plan catalogue says (PlanFeatureFilter).
public static class AdminFeatureEndpoints
{
    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/features")]
    public static async Task<IReadOnlyList<AdminFeature>> List(IBillingStore billing, CancellationToken cancellationToken)
    {
        var overrides = await billing.FeatureOverridesAsync(cancellationToken);
        bool? Override(string scope, string key, string feature)
            => overrides.FirstOrDefault(o => o.Id == FeatureOverride.IdFor(scope, key, feature))?.Enabled;

        return [.. PlanFeatures.Descriptions.Select(feature => new AdminFeature(
            feature.Key,
            feature.Value,
            Override(FeatureScopes.Global, string.Empty, feature.Key),
            [.. PlanCatalogue.Offered.Select(plan => new AdminPlanFeature(plan.Id, plan.Name, plan.Includes(feature.Key), Override(FeatureScopes.Plan, plan.Id, feature.Key)))],
            overrides.Count(o => o.Feature == feature.Key && o.Scope == FeatureScopes.Organisation)))];
    }

    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePut("/api/admin/features/{feature}")]
    public static async Task<Results<NoContent, NotFound>> Set(
        string feature, SetFeatureRequest request, ClaimsPrincipal principal, IBillingStore billing, IAuditLog auditLog, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!PlanFeatures.Descriptions.ContainsKey(feature))
        {
            return TypedResults.NotFound();
        }

        var key = request.Scope == FeatureScopes.Global ? string.Empty : request.ScopeKey!.Trim();
        var id = FeatureOverride.IdFor(request.Scope, key, feature);
        await billing.SetFeatureOverrideAsync(
            id,
            request.Enabled is { } enabled
                ? new FeatureOverride { Id = id, Feature = feature, Scope = request.Scope, ScopeKey = key, Enabled = enabled, UpdatedAt = clock.GetUtcNow() }
                : null,
            cancellationToken);
        var target = request.Scope == FeatureScopes.Global ? "everyone" : $"{request.Scope.ToLowerInvariant()} {key}";
        var change = request.Enabled switch { true => "on", false => "off", null => "as the plans say" };
        await auditLog.RecordAsync(principal, AuditActions.SetFeature, "Feature", feature, $"{change} for {target}", cancellationToken);
        return TypedResults.NoContent();
    }
}
