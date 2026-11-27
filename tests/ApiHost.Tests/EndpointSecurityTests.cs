using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiHost.Tests;

// Identity only (excluded otherwise by template.json). Checks the security attributes of every
// endpoint, so a new endpoint that forgets [Authorize] or rate limiting fails here.
[Collection("api")]
public class EndpointSecurityTests
{
    // Endpoints that check a password or a one-time token, send email, or create an account.
    private static readonly string[] RateLimited =
    [
        "POST /api/auth/login",
        "POST /api/auth/register",
        "POST /api/auth/confirm-email",
        "POST /api/auth/resend-confirmation",
        "POST /api/auth/confirm-email-change",
        "POST /api/auth/forgot-password",
        "POST /api/auth/reset-password",
        "POST /api/auth/accept-invitation",
        "POST /api/auth/passkey/options",
        "POST /api/auth/passkey/login",
        "GET /api/auth/external/{provider}",
        "POST /api/account/password",
        "POST /api/account/delete",
        "POST /api/account/passkeys",
    ];

    // Endpoints under /api/auth that anyone may call (they're how people sign in).
    private static readonly string[] Anonymous =
    [
        "POST /api/auth/login",
        "POST /api/auth/register",
        "POST /api/auth/refresh",
        "POST /api/auth/logout",
        "POST /api/auth/confirm-email",
        "POST /api/auth/resend-confirmation",
        "POST /api/auth/confirm-email-change",
        "POST /api/auth/forgot-password",
        "POST /api/auth/reset-password",
        "POST /api/auth/accept-invitation",
        "GET /api/auth/options",
        "POST /api/auth/passkey/options",
        "POST /api/auth/passkey/login",
        "GET /api/auth/external/{provider}",
        "GET /api/auth/external/callback",
    ];

    private readonly AppFixture _fixture;

    public EndpointSecurityTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private Dictionary<string, RouteEndpoint> Endpoints()
    {
        var endpoints = _fixture.Host.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true);

        var byRoute = new Dictionary<string, RouteEndpoint>(StringComparer.Ordinal);
        foreach (var endpoint in endpoints)
        {
            foreach (var method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
            {
                byRoute[$"{method} {endpoint.RoutePattern.RawText}"] = endpoint;
            }
        }

        return byRoute;
    }

    private static bool RequiresLogin(Endpoint endpoint)
        => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0
           && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;

    [Fact]
    public void every_account_and_admin_endpoint_requires_a_login()
    {
        var unprotected = Endpoints()
            .Where(e => e.Key.Contains("/api/account/", StringComparison.Ordinal) || e.Key.Contains("/api/admin/", StringComparison.Ordinal))
            .Where(e => !RequiresLogin(e.Value))
            .Select(e => e.Key);

        Assert.Empty(unprotected);
    }

    [Fact]
    public void every_admin_endpoint_requires_the_admin_role()
    {
        var admin = Endpoints().Where(e => e.Key.Contains("/api/admin/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(admin);

        var notAdminOnly = admin
            .Where(e => !e.Value.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Roles == AppRoles.Admin))
            .Select(e => e.Key);

        Assert.Empty(notAdminOnly);
    }

    [Fact]
    public void only_the_sign_in_endpoints_are_open_to_anyone()
    {
        // Everything else under /api/auth needs a login (for example /api/auth/me).
        var open = Endpoints()
            .Where(e => e.Key.Contains("/api/auth/", StringComparison.Ordinal) && !RequiresLogin(e.Value))
            .Select(e => e.Key)
            .Order(StringComparer.Ordinal);

        Assert.Equal(Anonymous.Order(StringComparer.Ordinal), open);
    }

    [Fact]
    public void endpoints_that_check_secrets_or_send_email_are_rate_limited()
    {
        var endpoints = Endpoints();
        var missing = RateLimited.Where(route =>
            !endpoints.TryGetValue(route, out var endpoint)
            || endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName != AuthRateLimiting.Policy);

        Assert.Empty(missing);
    }
}
