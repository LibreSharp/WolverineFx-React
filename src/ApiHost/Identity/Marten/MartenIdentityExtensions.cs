using Marten;
using Marten.AspNetIdentity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity.Marten;

public static class MartenIdentityExtensions
{
    public static void AddApplicationIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddMartenStores<ApplicationUser, IdentityRole>()
            .AddDefaultTokenProviders();

        services.AddScoped<IRefreshTokenStore, MartenRefreshTokenStore>();

        // Keep Data Protection keys in the database rather than the container's file system, so
        // password reset links survive restarts and work on every instance of the app.
        services.AddDataProtection().SetApplicationName("ApiHost");
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new ConfigureOptions<KeyManagementOptions>(options =>
                options.XmlRepository = new MartenDataProtectionKeyRepository(sp.GetRequiredService<IDocumentStore>())));
    }
}
