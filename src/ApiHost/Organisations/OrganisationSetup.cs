using ApiHost.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace ApiHost.Organisations;

public static class OrganisationSetup
{
    public static void AddOrganisations(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOrganisationStore, OrganisationStore>();
        services.AddScoped<OrganisationAccess>();
        services.ConfigureWolverine(opts =>
        {
            opts.CodeGeneration.AlwaysUseServiceLocationFor<OrganisationAccess>();
            opts.CodeGeneration.AlwaysUseServiceLocationFor<IOrganisationStore>();
        });

        // An access token is only accepted while its user is still a member of its organisation,
        // so removing someone, or deleting the organisation, takes effect at once rather than when
        // the token expires. (One indexed query per request.) The browser then refreshes its
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
                    || await store.FindMembershipAsync(id, principal.GetUserId(), context.HttpContext.RequestAborted) is null)
                {
                    context.Fail("No longer a member of the token's organisation");
                }
            };
        });
    }
}
