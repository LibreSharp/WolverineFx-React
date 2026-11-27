using System.Net;
using System.Security.Claims;
using Alba;
using ApiHost.Identity;
using ApiHost.Identity.Endpoints;
using ApiHost.Identity.External;
using ApiHost.Identity.Passkeys;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ApiHost.Tests;

// Identity only (excluded otherwise by template.json): adding passkeys and logging in with them,
// using SoftwarePasskey in place of a browser and authenticator.
[Collection("api")]
public class PasskeyTests
{
    private const string Password = "P@ssword123!";

    private readonly AppFixture _fixture;

    public PasskeyTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private async Task<string> SignedInUserAsync()
    {
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        return (await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, Password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!.AccessToken;
    }

    private async Task AddPasskeyAsync(string accessToken, SoftwarePasskey passkey, string? name = "Laptop")
    {
        var options = (await _fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/account/passkeys/options");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<PasskeyOptionsResponse>()!;

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new AddPasskeyRequest(passkey.Register(options.OptionsJson), options.State, name)).ToUrl("/api/account/passkeys");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });
    }

    private async Task<PasskeyOptionsResponse> LoginOptionsAsync() =>
        (await _fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/auth/passkey/options");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<PasskeyOptionsResponse>()!;

    private Task<IScenarioResult> PasskeyLoginAsync(string credentialJson, string state, HttpStatusCode expected) =>
        _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new PasskeyLoginRequest(credentialJson, state)).ToUrl("/api/auth/passkey/login");
            s.StatusCodeShouldBe(expected);
        });

    private async Task<IReadOnlyList<PasskeySummary>> ListAsync(string accessToken) =>
        (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/account/passkeys");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<List<PasskeySummary>>()!;

    [Fact]
    public async Task a_passkey_added_in_settings_logs_in_without_a_password()
    {
        var accessToken = await SignedInUserAsync();
        using var passkey = new SoftwarePasskey();
        await AddPasskeyAsync(accessToken, passkey);

        var saved = Assert.Single(await ListAsync(accessToken));
        Assert.Equal(passkey.Id, saved.Id);
        Assert.Equal("Laptop", saved.Name);

        var options = await LoginOptionsAsync();
        var login = (await PasskeyLoginAsync(passkey.Authenticate(options.OptionsJson), options.State, HttpStatusCode.OK)).ReadAsJson<LoginResponse>()!;

        var me = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/me");
            s.WithBearerToken(login.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<MeResponse>()!;
        var owner = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/me");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<MeResponse>()!;
        Assert.Equal(owner.UserId, me.UserId);
    }

    [Fact]
    public async Task a_replayed_passkey_response_is_refused()
    {
        var accessToken = await SignedInUserAsync();
        using var passkey = new SoftwarePasskey();
        await AddPasskeyAsync(accessToken, passkey);

        var options = await LoginOptionsAsync();
        var response = passkey.Authenticate(options.OptionsJson);
        await PasskeyLoginAsync(response, options.State, HttpStatusCode.OK);

        // Same signature again: its counter hasn't moved on, so Identity rejects it.
        await PasskeyLoginAsync(response, options.State, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task a_response_from_another_site_is_refused()
    {
        var accessToken = await SignedInUserAsync();
        using var passkey = new SoftwarePasskey();
        await AddPasskeyAsync(accessToken, passkey);

        var options = await LoginOptionsAsync();
        await PasskeyLoginAsync(passkey.Authenticate(options.OptionsJson, origin: "https://evil.example"), options.State, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task registration_state_from_another_user_is_refused()
    {
        var mine = await SignedInUserAsync();
        var someoneElse = await SignedInUserAsync();
        var options = (await _fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/account/passkeys/options");
            s.WithBearerToken(someoneElse);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<PasskeyOptionsResponse>()!;
        using var passkey = new SoftwarePasskey();

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new AddPasskeyRequest(passkey.Register(options.OptionsJson), options.State, null)).ToUrl("/api/account/passkeys");
            s.WithBearerToken(mine);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task a_locked_account_cannot_log_in_with_a_passkey()
    {
        var accessToken = await SignedInUserAsync();
        using var passkey = new SoftwarePasskey();
        await AddPasskeyAsync(accessToken, passkey);
        var userId = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/me");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<MeResponse>()!.UserId;
        using (var scope = _fixture.Host.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await userManager.SetLockoutEndDateAsync((await userManager.FindByIdAsync(userId))!, Lockout.Indefinitely);
        }

        var options = await LoginOptionsAsync();
        await PasskeyLoginAsync(passkey.Authenticate(options.OptionsJson), options.State, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task passkeys_can_be_renamed_and_removed_but_not_the_last_way_to_sign_in()
    {
        // An account with no password: signed up with Google, then added a passkey.
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.Email, NewEmail()), new Claim(ExternalProviders.EmailVerifiedClaim, "true")], "Google");
        string accessToken;
        using (var scope = _fixture.Host.Services.CreateScope())
        {
            var signIn = await scope.ServiceProvider.GetRequiredService<ExternalLoginService>().SignInAsync(
                new ExternalLoginInfo(new ClaimsPrincipal(claims), "Google", Guid.NewGuid().ToString("N"), "Google"), CancellationToken.None);
            accessToken = (await scope.ServiceProvider.GetRequiredService<TokenIssuer>().IssueAsync(signIn.User!, CancellationToken.None)).AccessToken;
        }

        using var passkey = new SoftwarePasskey();
        await AddPasskeyAsync(accessToken, passkey);

        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new RenamePasskeyRequest("Phone")).ToUrl($"/api/account/passkeys/{passkey.Id}");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        Assert.Equal("Phone", Assert.Single(await ListAsync(accessToken)).Name);

        // Google can go, since the passkey remains...
        await _fixture.Host.Scenario(s =>
        {
            s.Delete.Url("/api/account/external-logins/Google");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        // ...but then the passkey is the only way in.
        await _fixture.Host.Scenario(s =>
        {
            s.Delete.Url($"/api/account/passkeys/{passkey.Id}");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task a_passkey_can_be_removed_while_a_password_remains()
    {
        var accessToken = await SignedInUserAsync();
        using var passkey = new SoftwarePasskey();
        await AddPasskeyAsync(accessToken, passkey);

        await _fixture.Host.Scenario(s =>
        {
            s.Delete.Url($"/api/account/passkeys/{passkey.Id}");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        Assert.Empty(await ListAsync(accessToken));
        var options = await LoginOptionsAsync();
        await PasskeyLoginAsync(passkey.Authenticate(options.OptionsJson), options.State, HttpStatusCode.Unauthorized);
    }
}
