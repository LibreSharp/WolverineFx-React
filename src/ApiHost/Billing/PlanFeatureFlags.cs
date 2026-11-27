using Microsoft.FeatureManagement;

namespace ApiHost.Billing;

// Plan features are feature flags (Microsoft.FeatureManagement): each feature in PlanFeatures is a
// flag that the "Plan" filter turns on for organisations whose plan includes it. Other flags come
// from configuration as usual (the "FeatureManagement" section), so Entitlements.IsEnabledAsync
// answers both "is this on?" and "is this in their plan?".
public class PlanFeatureDefinitions(IConfiguration configuration) : IFeatureDefinitionProvider
{
    private readonly ConfigurationFeatureDefinitionProvider _configured = new(configuration);

    public async Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
        => PlanFeatures.Descriptions.ContainsKey(featureName)
            ? PlanFeature(featureName)
            : await _configured.GetFeatureDefinitionAsync(featureName);

    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
    {
        foreach (var name in PlanFeatures.Descriptions.Keys)
        {
            yield return PlanFeature(name);
        }

        await foreach (var definition in _configured.GetAllFeatureDefinitionsAsync())
        {
            if (!PlanFeatures.Descriptions.ContainsKey(definition.Name))
            {
                yield return definition;
            }
        }
    }

    private static FeatureDefinition PlanFeature(string name)
        => new() { Name = name, EnabledFor = [new FeatureFilterConfiguration { Name = PlanFeatureFilter.Alias }] };
}

// The organisation a feature is checked for, the plan it's on now, and the back office's
// overrides.
public record OrganisationFeatureContext(Guid OrganisationId, Plan Plan, IReadOnlyList<FeatureOverride> Overrides);

// A feature turned on or off by an admin (the back office's feature flags), over what the plans
// say. The most specific wins: an organisation's, then everyone's, then a plan's, then the plan
// catalogue. Id: "{Scope}:{ScopeKey}:{Feature}".
public class FeatureOverride
{
    public string Id { get; set; } = string.Empty;

    public string Feature { get; set; } = string.Empty;

    // One of FeatureScopes.
    public string Scope { get; set; } = string.Empty;

    // The plan's id or the organisation's id; empty for Global.
    public string ScopeKey { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static string IdFor(string scope, string scopeKey, string feature) => $"{scope}:{scopeKey}:{feature}";
}

public static class FeatureScopes
{
    public const string Global = "Global";
    public const string Plan = "Plan";
    public const string Organisation = "Organisation";

    public static readonly IReadOnlyList<string> All = [Global, Plan, Organisation];
}

[FilterAlias(Alias)]
public class PlanFeatureFilter : IContextualFeatureFilter<OrganisationFeatureContext>
{
    public const string Alias = "Plan";

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext featureFilterContext, OrganisationFeatureContext appContext)
        => Task.FromResult(IsEnabled(featureFilterContext.FeatureName, appContext));

    public static bool IsEnabled(string feature, OrganisationFeatureContext context)
    {
        FeatureOverride? Find(string scope, string key)
            => context.Overrides.FirstOrDefault(o => o.Id == FeatureOverride.IdFor(scope, key, feature));

        var chosen = Find(FeatureScopes.Organisation, context.OrganisationId.ToString())
            ?? Find(FeatureScopes.Global, string.Empty)
            ?? Find(FeatureScopes.Plan, context.Plan.Id);
        return chosen?.Enabled ?? context.Plan.Includes(feature);
    }
}
