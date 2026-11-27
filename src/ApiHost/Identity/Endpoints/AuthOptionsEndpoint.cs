using Microsoft.Extensions.Options;
using Wolverine.Http;

namespace ApiHost.Identity.Endpoints;

// What the login and registration pages should offer.
public record AuthOptionsResponse(bool AllowRegistration);

public static class AuthOptionsEndpoint
{
    [WolverineGet("/api/auth/options")]
    public static AuthOptionsResponse Get(IOptionsSnapshot<AccountOptions> accountOptions)
        => new(accountOptions.Value.AllowRegistration);
}
