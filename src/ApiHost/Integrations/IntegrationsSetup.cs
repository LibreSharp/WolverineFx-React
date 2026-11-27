using System.Security.Claims;
using System.Threading.RateLimiting;
using ApiHost.Integrations.ApiKeys;
using ApiHost.Integrations.Mcp;
using ApiHost.Integrations.Secrets;
using ApiHost.Integrations.Webhooks;
using ApiHost.Organisations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;
using Wolverine.ErrorHandling;

namespace ApiHost.Integrations;

// Ways for other programs to work with an organisation's data: API keys, an MCP endpoint for AI
// assistants, outgoing webhooks, and encrypted secrets. See docs/integrations.md.
public static class IntegrationsSetup
{
    public const string McpPath = "/mcp";

    // The default authentication scheme: an "Authorization: Bearer sk_..." request is checked as an
    // API key, anything else as a JWT.
    private const string BearerOrApiKey = "BearerOrApiKey";

    public static void AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));
        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.SectionName));
        services.Configure<IncomingWebhookOptions>(configuration.GetSection(IncomingWebhookOptions.SectionName));
        services.AddScoped<IApiKeyStore, ApiKeyStore>();
        services.AddScoped<IWebhookStore, WebhookStore>();
        services.AddScoped<ISecretStore, SecretStore>();
        services.AddScoped<OrganisationSecrets>();
        services.AddSingleton<SecretProtection>();
        services.AddScoped<WebhookSender>();
        services.AddScoped<ApiHost.DataExport.IOrganisationDataSource, IntegrationsExportData>();
        WebhookSender.AddClient(services);
        services.ConfigureWolverine(opts =>
        {
            opts.CodeGeneration.AlwaysUseServiceLocationFor<IApiKeyStore>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<IWebhookStore>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<ISecretStore>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<OrganisationSecrets>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<WebhookSender>();

            // A receiver that fails gets the delivery again later: after a minute, then 5 and 30
            // minutes, 2 hours and 8 hours. Then it's parked with the failed messages (the back
            // office), and an owner can still resend it.
            opts.OnException<WebhookDeliveryFailedException>().ScheduleRetry(
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(8));
        });

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { })
            // No display name, so it isn't offered as a sign-in provider (Identity lists schemes that have one).
            .AddPolicyScheme(BearerOrApiKey, displayName: null, options => options.ForwardDefaultSelector = context =>
                ApiKeyAuthenticationHandler.KeyFrom(context.Request) is null
                    ? JwtBearerDefaults.AuthenticationScheme
                    : ApiKeyAuthenticationHandler.SchemeName);

        // After IdentityJwtSetup's (and Identity's own) defaults.
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = BearerOrApiKey;
            options.DefaultAuthenticateScheme = BearerOrApiKey;
            options.DefaultChallengeScheme = BearerOrApiKey;
        });

        // Each key gets ApiKeyOptions.RequestsPerMinute; logins aren't limited here.
        var perMinute = configuration.GetValue($"{ApiKeyOptions.SectionName}:{nameof(ApiKeyOptions.RequestsPerMinute)}", new ApiKeyOptions().RequestsPerMinute);
        services.Configure<RateLimiterOptions>(options => options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.User.FindFirstValue(ApiKeyClaims.KeyId) is { } keyId
                ? RateLimitPartition.GetFixedWindowLimiter(keyId, _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) })
                : RateLimitPartition.GetNoLimiter(string.Empty)));

        // The MCP endpoint (the official C# SDK, Apache-2.0). Stateless: each request stands alone,
        // so it works behind a load balancer with no session affinity.
        services.AddHttpContextAccessor();
        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<TodoTools>();
    }

    // After UseAuthentication, before UseAuthorization.
    public static void UseApiKeyRestrictions(this IApplicationBuilder app) => app.UseMiddleware<ApiKeyRestrictions>();

    // Only API keys can use the MCP endpoint.
    public static void MapIntegrations(this IEndpointRouteBuilder app)
        => app.MapMcp(McpPath).RequireAuthorization(new AuthorizationPolicyBuilder(ApiKeyAuthenticationHandler.SchemeName).RequireAuthenticatedUser().Build());
}

// An organisation's keys, webhooks and secrets go with it.
public static class DeleteIntegrationsOfDeletedOrganisationHandler
{
    public static async Task Handle(OrganisationDeleted message, IApiKeyStore keys, IWebhookStore webhooks, ISecretStore secrets, CancellationToken cancellationToken)
    {
        await keys.DeleteOrganisationAsync(message.OrganisationId, cancellationToken);
        await webhooks.DeleteOrganisationAsync(message.OrganisationId, cancellationToken);
        await secrets.DeleteOrganisationAsync(message.OrganisationId, cancellationToken);
    }
}
