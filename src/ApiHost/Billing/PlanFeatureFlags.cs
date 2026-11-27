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

// The organisation a feature is checked for, and the plan it's on now.
public record OrganisationFeatureContext(Guid OrganisationId, Plan Plan);

[FilterAlias(Alias)]
public class PlanFeatureFilter : IContextualFeatureFilter<OrganisationFeatureContext>
{
    public const string Alias = "Plan";

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext featureFilterContext, OrganisationFeatureContext appContext)
        => Task.FromResult(appContext.Plan.Includes(featureFilterContext.FeatureName));
}
