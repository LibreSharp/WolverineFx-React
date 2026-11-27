using System.Threading.RateLimiting;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace ApiHost.Organisations;

public static class OrganisationSetup
{
    // The rate limit on sending invitations (OrganisationOptions.InvitationsPerHour), per user.
    public const string InvitationsPolicy = "invitations";

    public static void AddOrganisations(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<OrganisationOptions>(configuration.GetSection(OrganisationOptions.SectionName));

        // Per user (the rate limiter runs after authentication), so it can't be dodged by
        // switching organisation or address.
        var invitationsPerHour = configuration.GetValue($"{OrganisationOptions.SectionName}:{nameof(OrganisationOptions.InvitationsPerHour)}", new OrganisationOptions().InvitationsPerHour);
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(InvitationsPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(context.User.Identity?.IsAuthenticated == true ? context.User.GetUserId() : "anonymous", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = invitationsPerHour,
                Window = TimeSpan.FromHours(1),
            })));
        services.AddScoped<IOrganisationStore, OrganisationStore>();
        services.AddScoped<OrganisationAccess>();
        services.ConfigureWolverine(opts =>
        {
            opts.CodeGeneration.AlwaysUseServiceLocationFor<OrganisationAccess>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<IOrganisationStore>();
        });

        // An access token is only accepted while its user is still a member of its organisation,
        // and the organisation isn't suspended, so removing someone, or deleting or suspending the
        // organisation, takes effect at once rather than when the token expires. (One indexed query per request.) The browser then refreshes its
        // tokens, which picks another of the user's organisations.
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var validated = options.Events?.OnTokenValidated;
            options.Events ??= new JwtBearerEvents();
            options.Events.OnTokenValidated = async context =>
            {
                if (validated is not null)
                {
                    await validated(context);
                }

                var principal = context.Principal!;
                var organisationId = principal.FindFirst(OrganisationClaims.OrganisationId)?.Value;
                var store = context.HttpContext.RequestServices.GetRequiredService<IOrganisationStore>();
                if (!Guid.TryParse(organisationId, out var id)
                    || !await store.CanWorkInAsync(id, principal.GetUserId(), context.HttpContext.RequestAborted))
                {
                    context.Fail("No longer a member of the token's organisation, or it's suspended");
                }
            };
        });
    }
}
