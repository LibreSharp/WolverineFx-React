using Api.Data.EfCore;
using Microsoft.AspNetCore.Identity;

namespace Api.Identity.EfCore;

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
