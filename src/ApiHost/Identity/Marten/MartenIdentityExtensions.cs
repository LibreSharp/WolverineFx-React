using Marten;
using Marten.AspNetIdentity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity.Marten;

public static class MartenIdentityExtensions
{
    public static IdentityBuilder AddApplicationIdentity(this IServiceCollection services)
    {
        // Marten.AspNetIdentity's stores, with the user store extended (see ApplicationUserStore).
        var identity = services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddUserStore<ApplicationUserStore>()
            .AddRoleStore<MartenRoleStore<IdentityRole>>();

        services.AddScoped<IRefreshTokenStore, MartenRefreshTokenStore>();

        // Keep Data Protection keys in the database rather than the container's file system, so
        // password reset links survive restarts and work on every instance of the app.
        services.AddDataProtection().SetApplicationName("ApiHost");
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new ConfigureOptions<KeyManagementOptions>(options =>
                options.XmlRepository = new MartenDataProtectionKeyRepository(sp.GetRequiredService<IDocumentStore>())));

        return identity;
    }
}
