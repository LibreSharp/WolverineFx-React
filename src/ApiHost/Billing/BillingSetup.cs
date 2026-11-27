using ApiHost.Features.Todos;
using ApiHost.Organisations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using Wolverine;

namespace ApiHost.Billing;

public static class BillingSetup
{
    public static void AddBilling(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.AddScoped<IBillingStore, BillingStore>();
        services.AddScoped<Entitlements>();

        // What counts towards each Count limit (PlanLimits).
        services.AddScoped<ILimitUsage, OpenTodosUsage>();
        services.AddScoped<ILimitUsage, MembersUsage>();

        // Plan features as feature flags (PlanFeatureFlags.cs). Registered before
        // AddFeatureManagement, which only adds its own definitions (from configuration) if there
        // are none.
        services.AddSingleton<IFeatureDefinitionProvider, PlanFeatureDefinitions>();
        services.AddFeatureManagement().AddFeatureFilter<PlanFeatureFilter>();

        services.ConfigureWolverine(opts =>
        {
            opts.CodeGeneration.AlwaysUseServiceLocationFor<Entitlements>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<IBillingStore>();
        });
    }
}

// A deleted organisation's billing account and usage go with it.
public static class DeleteBillingOfDeletedOrganisationHandler
{
    public static Task Handle(OrganisationDeleted message, IBillingStore store, CancellationToken cancellationToken)
        => store.DeleteOrganisationAsync(message.OrganisationId, cancellationToken);
}
