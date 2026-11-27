using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity;

// At startup: creates the roles in AppRoles.All that don't exist yet, and makes the confirmed
// accounts listed in Accounts:AdminEmails admins. (TokenIssuer does the same for an admin email
// that registers later, when it first logs in.)
public class RoleSeeder(IServiceProvider services, IOptions<AccountOptions> options, ILogger<RoleSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var email in options.Value.AdminEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null && await userManager.MakeAdminIfConfiguredAsync(user, options.Value))
            {
                logger.LogInformation("Made {Email} an admin (Accounts:AdminEmails)", email);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class AdminEmails
{
    // Adds the Admin role if the user's email is in Accounts:AdminEmails and confirmed (so nobody
    // can claim admin by registering with that address). Returns true if it was added.
    public static async Task<bool> MakeAdminIfConfiguredAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user, AccountOptions options)
    {
        if (!user.EmailConfirmed || !options.IsAdminEmail(user.Email) || await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return false;
        }

        return (await userManager.AddToRoleAsync(user, AppRoles.Admin)).Succeeded;
    }
}
