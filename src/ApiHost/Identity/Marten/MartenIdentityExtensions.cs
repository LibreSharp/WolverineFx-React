using Marten.AspNetIdentity;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity.Marten;

public static class MartenIdentityExtensions
{
    public static void AddApplicationIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddMartenStores<ApplicationUser, IdentityRole>()
            .AddDefaultTokenProviders();

        services.AddScoped<IRefreshTokenStore, MartenRefreshTokenStore>();
    }
}
