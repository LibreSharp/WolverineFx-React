using System.Net;
using Alba;
using ApiHost.Identity.Endpoints;

namespace ApiHost.Tests;

[Collection("api")]
public class AuthEndpointsTests
{
    private readonly AppFixture _fixture;

    public AuthEndpointsTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private static (string Email, string Password) NewCredentials() =>
        ($"{Guid.NewGuid():N}@example.com", "P@ssword123!");

    private async Task RegisterAsync(string email, string password)
    {
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterRequest(email, password)).ToUrl("/api/auth/register");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });
    }

    [Fact]
    public async Task register_then_login_returns_tokens()
    {
        var (email, password) = NewCredentials();
        await RegisterAsync(email, password);

        var login = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        });

        var response = login.ReadAsJson<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(response!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public async Task login_with_wrong_password_is_unauthorized()
    {
        var (email, password) = NewCredentials();
        await RegisterAsync(email, password);

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, "not-the-password")).ToUrl("/api/auth/login");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    [Fact]
    public async Task access_token_authorizes_the_me_endpoint()
    {
        var (email, password) = NewCredentials();
        await RegisterAsync(email, password);

        var login = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        });
        var tokens = login.ReadAsJson<LoginResponse>();

        var me = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/me");
            s.WithBearerToken(tokens!.AccessToken);
            s.StatusCodeShouldBeOk();
        });

        var meResponse = me.ReadAsJson<MeResponse>();
        Assert.Equal(email, meResponse!.Email);
    }

    [Fact]
    public async Task refresh_rotates_the_refresh_token_and_the_old_one_stops_working()
    {
        var (email, password) = NewCredentials();
        await RegisterAsync(email, password);

        var login = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        });
        var original = login.ReadAsJson<LoginResponse>();

        var refresh = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(original!.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBeOk();
        });
        var rotated = refresh.ReadAsJson<RefreshResponse>();
        Assert.NotEqual(original!.RefreshToken, rotated!.RefreshToken);

        // Reusing the now-rotated-away token must be rejected.
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(original.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });

        // Reuse detection revokes the whole chain, so even the freshly-rotated token is now dead.
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(rotated.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    [Fact]
    public async Task logout_revokes_the_refresh_token()
    {
        var (email, password) = NewCredentials();
        await RegisterAsync(email, password);

        var login = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        });
        var tokens = login.ReadAsJson<LoginResponse>();

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LogoutRequest(tokens!.RefreshToken)).ToUrl("/api/auth/logout");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(tokens!.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }
}
