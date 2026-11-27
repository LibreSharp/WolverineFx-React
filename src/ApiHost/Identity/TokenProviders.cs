using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApiHost.Identity;

// One-time tokens for the links in account emails, created and checked by Identity's
// DataProtectorTokenProvider. Each kind of link gets its own provider, so each has its own
// lifetime: email confirmation and email change links use Identity's default provider (valid
// for a day); password reset links are valid for an hour. Every token also stops working once
// it has been used, because using it changes the user's security stamp.
public static class TokenProviders
{
    public const string PasswordReset = nameof(PasswordReset);

    public static IdentityBuilder AddAppTokenProviders(this IdentityBuilder identity)
    {
        identity.AddDefaultTokenProviders();
        identity.AddTokenProvider<PasswordResetTokenProvider>(PasswordReset);
        identity.Services.Configure<PasswordResetTokenProviderOptions>(options =>
        {
            options.Name = PasswordReset;
            options.TokenLifespan = TimeSpan.FromHours(1);
        });
        identity.Services.Configure<IdentityOptions>(options => options.Tokens.PasswordResetTokenProvider = PasswordReset);
        return identity;
    }
}

public class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions;

public class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger);
