using ApiHost.Data.EfCore;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity.EfCore;

public static class EfCoreIdentityExtensions
{
    public static void AddApplicationIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IRefreshTokenStore, EfCoreRefreshTokenStore>();
    }
}
