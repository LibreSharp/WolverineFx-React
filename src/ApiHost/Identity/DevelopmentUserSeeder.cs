using Microsoft.AspNetCore.Identity;

namespace ApiHost.Identity;

// Creates a ready-made login for local development, from the "DevelopmentUser" section of
// appsettings.Development.json. Registered only in the Development environment (see
// IdentityJwtSetup), so a deployed app never gets a user with a publicly known password.
public class DevelopmentUserSeeder(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<DevelopmentUserSeeder> logger) : IHostedService
{
    public const string SectionName = "DevelopmentUser";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration[$"{SectionName}:Email"];
        var password = configuration[$"{SectionName}:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Created before email confirmation was required: confirm it so it can still log in.
            if (!existing.EmailConfirmed)
            {
                existing.EmailConfirmed = true;
                await userManager.UpdateAsync(existing);
            }

            return;
        }

        // Already confirmed, so it can log in without opening a confirmation email.
        var result = await userManager.CreateAsync(new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true }, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the development user '{email}': " +
                string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        logger.LogInformation("Created development user {Email} (see appsettings.Development.json)", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
