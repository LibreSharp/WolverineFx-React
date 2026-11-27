using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ApiHost.Identity;

// Slows down password guessing and account spam: each client IP gets a number of attempts a
// minute (Accounts:AuthAttemptsPerMinute, default 10) at the endpoints marked
// [EnableRateLimiting(AuthRateLimiting.Policy)] (login, register, email links, password reset
// and change, passkeys, account deletion); after that they get 429 Too Many Requests.
//
// Behind a reverse proxy, every request comes from the proxy's address unless the app reads the
// client's real address from X-Forwarded-For: set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (the
// production docker-compose file does), and only when the app is reachable through the proxy.
public static class AuthRateLimiting
{
    public const string Policy = "auth";

    public static void AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue($"{AccountOptions.SectionName}:AuthAttemptsPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, context => context.Connection.RemoteIpAddress is { } ip
                ? RateLimitPartition.GetFixedWindowLimiter(ip.ToString(), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                })
                // Only in-process test servers have no client address.
                : RateLimitPartition.GetNoLimiter("no-client-address"));
        });
    }
}
