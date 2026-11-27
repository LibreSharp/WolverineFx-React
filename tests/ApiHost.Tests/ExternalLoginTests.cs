using System.Net;
using System.Security.Claims;
using Alba;
using ApiHost.Identity;
using ApiHost.Identity.Endpoints;
using ApiHost.Identity.External;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiHost.Tests;

// Identity only (excluded otherwise by template.json): sign-in with Google or GitHub. The
// provider round trip can't run in tests, so these call ExternalLoginService with the
// ExternalLoginInfo a provider would produce, and check the endpoints with no provider configured.
[Collection("api")]
public class ExternalLoginTests
{
    private const string Password = "P@ssword123!";

    private readonly AppFixture _fixture;

    public ExternalLoginTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static ExternalLoginInfo FromGoogle(string? email, bool verified, string? key = null)
    {
        var claims = new List<Claim>();
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (verified)
        {
            claims.Add(new Claim(ExternalProviders.EmailVerifiedClaim, "True"));
        }

        return new ExternalLoginInfo(new ClaimsPrincipal(new ClaimsIdentity(claims, "Google")), "Google", key ?? Guid.NewGuid().ToString("N"), "Google");
    }

    private async Task<ExternalSignInResult> SignInAsync(ExternalLoginInfo info)
    {
        using var scope = _fixture.Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ExternalLoginService>().SignInAsync(info, CancellationToken.None);
    }

    private async Task<string> AccessTokenForAsync(ApplicationUser user)
    {
        using var scope = _fixture.Host.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<TokenIssuer>().IssueAsync(user, CancellationToken.None)).AccessToken;
    }

    private Task<IScenarioResult> LoginAsync(string email, string password, HttpStatusCode expected) =>
        _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBe(expected);
        });

    [Fact]
    public async Task a_verified_email_creates_a_confirmed_account_that_signs_in_again_by_its_login()
    {
        var email = NewEmail();
        var info = FromGoogle(email, verified: true);

        var first = await SignInAsync(info);
        Assert.NotNull(first.User);
        Assert.True(first.User.EmailConfirmed);

        // Same Google account again, even if its email has since changed.
        var again = await SignInAsync(FromGoogle(NewEmail(), verified: true, key: info.ProviderKey));
        Assert.Equal(first.User.Id, again.User!.Id);
    }

    [Fact]
    public async Task an_unverified_email_is_refused()
    {
        var email = NewEmail();

        var result = await SignInAsync(FromGoogle(email, verified: false));

        Assert.Null(result.User);
        Assert.Contains("verified email", result.Error, StringComparison.Ordinal);
        using var scope = _fixture.Host.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task a_verified_email_is_linked_to_the_existing_account_which_keeps_its_password()
    {
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);

        var result = await SignInAsync(FromGoogle(email, verified: true));

        var token = await AccessTokenForAsync(result.User!);
        var linked = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/account/external-logins");
            s.WithBearerToken(token);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<List<LinkedLogin>>()!;
        Assert.Equal("Google", Assert.Single(linked).Provider);
        await LoginAsync(email, Password, HttpStatusCode.OK);
    }

    [Fact]
    public async Task an_unconfirmed_account_loses_its_unproven_password_when_the_email_owner_signs_in()
    {
        // Someone registers the victim's email first, and never confirms it.
        var email = NewEmail();
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterRequest(email, Password)).ToUrl("/api/auth/register");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });

        var result = await SignInAsync(FromGoogle(email, verified: true));

        Assert.True(result.User!.EmailConfirmed);
        await LoginAsync(email, Password, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task a_locked_account_cannot_sign_in_with_a_provider()
    {
        var email = NewEmail();
        var info = FromGoogle(email, verified: true);
        var user = (await SignInAsync(info)).User!;
        using (var scope = _fixture.Host.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await userManager.SetLockoutEndDateAsync((await userManager.FindByIdAsync(user.Id))!, Lockout.Indefinitely);
        }

        var result = await SignInAsync(info);

        Assert.Null(result.User);
    }

    [Fact]
    public async Task with_registration_closed_a_provider_cannot_create_accounts()
    {
        var configuration = _fixture.Host.Services.GetRequiredService<IConfiguration>();
        configuration["Accounts:AllowRegistration"] = "false";
        try
        {
            var result = await SignInAsync(FromGoogle(NewEmail(), verified: true));
            Assert.Null(result.User);
        }
        finally
        {
            configuration["Accounts:AllowRegistration"] = "true";
        }
    }

    [Fact]
    public async Task a_login_linked_to_one_account_cannot_be_linked_to_another()
    {
        var info = FromGoogle(NewEmail(), verified: true);
        await SignInAsync(info);
        var otherEmail = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(otherEmail, Password);

        using var scope = _fixture.Host.Services.CreateScope();
        var other = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(otherEmail);
        var error = await scope.ServiceProvider.GetRequiredService<ExternalLoginService>().LinkAsync(other!, info);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task the_only_way_to_sign_in_cannot_be_removed_but_a_second_one_can()
    {
        var user = (await SignInAsync(FromGoogle(NewEmail(), verified: true))).User!;
        var token = await AccessTokenForAsync(user);

        await _fixture.Host.Scenario(s =>
        {
            s.Delete.Url("/api/account/external-logins/Google");
            s.WithBearerToken(token);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        var withPassword = (await SignInAsync(FromGoogle(email, verified: true))).User!;
        var withPasswordToken = await AccessTokenForAsync(withPassword);
        await _fixture.Host.Scenario(s =>
        {
            s.Delete.Url("/api/account/external-logins/Google");
            s.WithBearerToken(withPasswordToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
    }

    [Fact]
    public async Task only_configured_providers_are_offered_and_reachable()
    {
        // The fixture configures Google (with made-up credentials) but not GitHub.
        var options = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/options");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<AuthOptionsResponse>()!;
        Assert.Equal("Google", Assert.Single(options.ExternalProviders).Name);

        await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/external/GitHub");
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });
    }

    [Fact]
    public async Task starting_sign_in_redirects_to_the_provider_with_the_api_callback()
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/external/google");
            s.StatusCodeShouldBe(HttpStatusCode.Redirect);
        });

        var location = new Uri(result.Context.Response.Headers.Location.ToString());
        Assert.Equal("accounts.google.com", location.Host);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        Assert.EndsWith("/api/auth/external/signin-google", query["redirect_uri"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task linking_needs_a_valid_ticket_from_the_signed_in_user()
    {
        var token = await _fixture.CreateUserAndGetAccessTokenAsync();
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/account/external-logins/link-ticket");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
        var ticket = (await _fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/account/external-logins/link-ticket");
            s.WithBearerToken(token);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LinkTicketResponse>()!.Ticket;

        // A valid ticket goes on to the provider...
        var linking = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/auth/external/Google?link={Uri.EscapeDataString(ticket)}");
            s.StatusCodeShouldBe(HttpStatusCode.Redirect);
        });
        Assert.Equal("accounts.google.com", new Uri(linking.Context.Response.Headers.Location.ToString()).Host);

        // ...a forged one is sent back to Settings with an error.
        var forged = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/external/Google?link=not-a-ticket");
            s.StatusCodeShouldBe(HttpStatusCode.Redirect);
        });
        Assert.StartsWith("http://localhost:5173/settings#error=", forged.Context.Response.Headers.Location.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task the_callback_without_a_provider_sign_in_sends_the_browser_back_with_an_error()
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url(ExternalLoginEndpoints.CallbackPath);
            s.StatusCodeShouldBe(HttpStatusCode.Redirect);
        });

        Assert.StartsWith("http://localhost:5173/external-login#error=", result.Context.Response.Headers.Location.ToString(), StringComparison.Ordinal);
    }
}
