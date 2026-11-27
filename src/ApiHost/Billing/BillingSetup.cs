using ApiHost.Billing.Payments;
using ApiHost.Billing.Stripe;
using ApiHost.Features.Todos;
using ApiHost.Organisations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using Stripe;
using Wolverine;
using Wolverine.ErrorHandling;

namespace ApiHost.Billing;

public static class BillingSetup
{
    public static void AddBilling(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.AddScoped<IBillingStore, BillingStore>();

        // Payments through Stripe, once its keys are set (Billing:Stripe; docs/billing.md). Until
        // then there are no paid plans: just the trial and the free plan. To use another
        // provider, register its IPaymentProvider here instead.
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.AddSingleton<IPaymentProvider, StripePaymentProvider>();
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

            // Webhook events are handled from a durable local queue (as every local queue is; see
            // Program.cs), after the webhook has been answered. If Stripe can't be reached, the
            // message is tried again later, then parked in the dead-letter queue (the back
            // office's failed messages).
            opts.OnException<HttpRequestException>().Or<StripeException>(e => (int)e.HttpStatusCode is 0 or 429 or >= 500)
                .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));
        });
    }
}

// A deleted organisation's subscription is cancelled (it isn't charged again), and its billing
// account, usage and invoices go with it. The payment provider keeps its own records.
public static class DeleteBillingOfDeletedOrganisationHandler
{
    public static async Task Handle(OrganisationDeleted message, IBillingStore store, IPaymentProvider provider, CancellationToken cancellationToken)
    {
        var account = await store.FindAccountAsync(message.OrganisationId, cancellationToken);
        if (account?.SubscriptionId is { } subscriptionId
            && account.Status is SubscriptionStatuses.Active or SubscriptionStatuses.PastDue
            && provider.IsConfigured)
        {
            await provider.CancelSubscriptionAsync(subscriptionId, cancellationToken);
        }

        await store.DeleteOrganisationAsync(message.OrganisationId, cancellationToken);
    }
}
