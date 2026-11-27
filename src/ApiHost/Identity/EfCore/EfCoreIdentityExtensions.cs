using ApiHost.Data.EfCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity.EfCore;

public static class EfCoreIdentityExtensions
{
    public static IdentityBuilder AddApplicationIdentity(this IServiceCollection services)
    {
        var identity = services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IRefreshTokenStore, EfCoreRefreshTokenStore>();

        // Keep Data Protection keys in the database rather than the container's file system, so
        // password reset links survive restarts and work on every instance of the app.
        services.AddDataProtection()
            .SetApplicationName("ApiHost")
            .PersistKeysToDbContext<AppDbContext>();

        return identity;
    }
}
