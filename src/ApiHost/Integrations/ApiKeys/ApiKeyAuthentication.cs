using System.Security.Claims;
using System.Text.Encodings.Web;
using ApiHost.Organisations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ApiHost.Integrations.ApiKeys;

public static class ApiKeyClaims
{
    // Present only on requests made with an API key: the key's id, and its scope.
    public const string KeyId = "api_key";
    public const string Scope = "api_key_scope";

    public static bool IsApiKey(this ClaimsPrincipal user) => user.HasClaim(c => c.Type == KeyId);

    // A user's login can do everything; a key only what its scope allows.
    public static bool CanWrite(this ClaimsPrincipal user)
        => !user.IsApiKey() || user.FindFirstValue(Scope) == ApiKeyScopes.Write;
}

// Authenticates "Authorization: Bearer sk_..." requests. The request acts as the member who created
// the key, in the key's organisation, so tenancy, plan limits and realtime updates work as they do
// for that member's own requests. A key stops working when it's revoked or expires, when that member
// leaves, or when the organisation is suspended or deleted.
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyStore keys,
    IOrganisationStore organisations,
    TimeProvider clock) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    // How often LastUsedAt is written: not on every request.
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    public static string? KeyFrom(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer " + ApiKeySecrets.Prefix, StringComparison.Ordinal) ? header["Bearer ".Length..].Trim() : null;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (KeyFrom(Request) is not { } secret)
        {
            return AuthenticateResult.NoResult();
        }

        var key = await keys.FindByHashAsync(ApiKeySecrets.Hash(secret), Context.RequestAborted);
        var now = clock.GetUtcNow();
        if (key is null || key.IsExpired(now))
        {
            return AuthenticateResult.Fail("Unknown, revoked or expired API key");
        }

        if (!await organisations.CanWorkInAsync(key.OrganisationId, key.CreatedByUserId, Context.RequestAborted))
        {
            return AuthenticateResult.Fail("The key's creator has left its organisation, or it's suspended");
        }

        if (key.LastUsedAt is null || now - key.LastUsedAt > TouchInterval)
        {
            await keys.TouchAsync(key.Id, now, Context.RequestAborted);
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, key.CreatedByUserId),
                new Claim(OrganisationClaims.OrganisationId, key.OrganisationId.ToString()),
                new Claim(ApiKeyClaims.KeyId, key.Id.ToString()),
                new Claim(ApiKeyClaims.Scope, key.Scope),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

// Keeps API keys to the paths they're for (ApiKeyOptions.AllowedPaths), and read keys to reading.
// Runs after authentication, before the endpoint.
public class ApiKeyRestrictions(RequestDelegate next, IOptions<ApiKeyOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.IsApiKey())
        {
            var path = context.Request.Path;
            if (!options.Value.AllowedPaths.Any(allowed => path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase)))
            {
                await Refuse(context, "API keys can't be used here: sign in instead.");
                return;
            }

            // MCP requests are all POSTs: its tools check the scope themselves.
            var reads = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
            if (!reads && !user.CanWrite() && !path.StartsWithSegments(IntegrationsSetup.McpPath, StringComparison.OrdinalIgnoreCase))
            {
                await Refuse(context, "This API key can only read.");
                return;
            }
        }

        await next(context);
    }

    private static Task Refuse(HttpContext context, string detail)
        => Results.Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
}
