using System.Net;
using Alba;
using ApiHost.Identity.Endpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

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

    private Task RegisterAsync(string email, string password) => _fixture.RegisterConfirmedUserAsync(email, password);

    private async Task RegisterUnconfirmedAsync(string email, string password) =>
        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterRequest(email, password)).ToUrl("/api/auth/register");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        }));

    private Task<IScenarioResult> LoginAsync(string email, string password, HttpStatusCode expected) =>
        _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBe(expected);
        });

    [Fact]
    public async Task the_development_user_is_created_at_startup_and_can_log_in()
    {
        // The fixture runs the app in Development, which seeds DevelopmentUser from
        // appsettings.Development.json.
        var configuration = _fixture.Host.Services.GetRequiredService<IConfiguration>();
        var email = configuration["DevelopmentUser:Email"]!;
        var password = configuration["DevelopmentUser:Password"]!;

        var login = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        });

        Assert.False(string.IsNullOrWhiteSpace(login.ReadAsJson<LoginResponse>()!.AccessToken));
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
        Assert.NotEqual(original!.RefreshToken, rotated!.RefreshToken, StringComparer.Ordinal);

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

    [Fact]
    public async Task registering_emails_a_link_that_confirms_the_email_and_allows_login()
    {
        var (email, password) = NewCredentials();
        await RegisterUnconfirmedAsync(email, password);

        // Refused until the email is confirmed, with a reason the client can show.
        var refused = await LoginAsync(email, password, HttpStatusCode.Forbidden);
        Assert.Contains("EmailNotConfirmed", await refused.ReadAsTextAsync(), StringComparison.Ordinal);

        var link = _fixture.Emails.LinkFor(email, "/confirm-email")!;
        Assert.Equal("http://localhost:5173", link.GetLeftPart(UriPartial.Authority));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailRequest(query["userId"]!, query["token"]!)).ToUrl("/api/auth/confirm-email");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        await LoginAsync(email, password, HttpStatusCode.OK);
    }

    [Fact]
    public async Task an_unconfirmed_account_only_says_so_to_someone_who_knows_the_password()
    {
        var (email, password) = NewCredentials();
        await RegisterUnconfirmedAsync(email, password);

        await LoginAsync(email, "not-the-password", HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task an_invalid_confirmation_token_is_rejected()
    {
        var (email, password) = NewCredentials();
        await RegisterUnconfirmedAsync(email, password);
        var query = _fixture.Emails.LinkQuery(email, "/confirm-email");

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailRequest(query["userId"]!, "not-a-real-token")).ToUrl("/api/auth/confirm-email");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        await LoginAsync(email, password, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task the_confirmation_email_can_be_sent_again()
    {
        var (email, password) = NewCredentials();
        await RegisterUnconfirmedAsync(email, password);

        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ResendConfirmationRequest(email)).ToUrl("/api/auth/resend-confirmation");
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));

        Assert.Equal(2, _fixture.Emails.SentTo(email).Count);
    }

    [Fact]
    public async Task resending_a_confirmation_does_not_reveal_whether_an_email_is_registered()
    {
        var unknownEmail = NewCredentials().Email;

        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ResendConfirmationRequest(unknownEmail)).ToUrl("/api/auth/resend-confirmation");
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));

        Assert.Empty(_fixture.Emails.SentTo(unknownEmail));
    }
}
