using System.Net;
using Alba;
using ApiHost.Identity.Endpoints;
using Microsoft.AspNetCore.Http;

namespace ApiHost.Tests;

// Identity only (excluded otherwise by template.json).
[Collection("api")]
public class AuthRateLimitingTests
{
    private readonly AppFixture _fixture;

    public AuthRateLimitingTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task login_attempts_from_one_address_are_limited_to_ten_a_minute()
    {
        // A documentation-only address (RFC 5737), unique to this test so others aren't limited.
        var address = IPAddress.Parse("203.0.113.42");
        var statuses = new List<int>();

        for (var attempt = 1; attempt <= 11; attempt++)
        {
            var result = await _fixture.Host.Scenario(s =>
            {
                s.ConfigureHttpContext(context => context.Connection.RemoteIpAddress = address);
                s.Post.Json(new LoginRequest("nobody@example.com", "wrong-password")).ToUrl("/api/auth/login");
                s.IgnoreStatusCode();
            });
            statuses.Add(result.Context.Response.StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(StatusCodes.Status401Unauthorized, status));
        Assert.Equal(StatusCodes.Status429TooManyRequests, statuses[10]);
    }
}
