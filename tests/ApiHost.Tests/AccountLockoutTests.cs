using System.Net;
using Alba;
using ApiHost.Identity;
using ApiHost.Identity.Endpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// Identity only (excluded otherwise by template.json): lockout after repeated failed logins.
[Collection("api")]
public class AccountLockoutTests
{
    private const string Password = "P@ssword123!";

    private readonly AppFixture _fixture;

    public AccountLockoutTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private Task<IScenarioResult> LoginAsync(string email, string password, HttpStatusCode expected) =>
        _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBe(expected);
        });

    private async Task FailLoginsAsync(string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await LoginAsync(email, "not-the-password", HttpStatusCode.Unauthorized);
        }
    }

    private async Task<ApplicationUser> FindUserAsync(string email)
    {
        using var scope = _fixture.Host.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!;
    }

    [Fact]
    public async Task five_wrong_passwords_lock_the_account_for_15_minutes()
    {
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);

        await FailLoginsAsync(email, 4);
        await LoginAsync(email, "not-the-password", HttpStatusCode.Forbidden);

        // Even the right password is refused now, with the reason.
        var refused = await LoginAsync(email, Password, HttpStatusCode.Forbidden);
        Assert.Contains("LockedOut", await refused.ReadAsTextAsync(), StringComparison.Ordinal);

        var lockoutEnd = (await FindUserAsync(email)).LockoutEnd;
        Assert.InRange(lockoutEnd!.Value, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
    }

    [Fact]
    public async Task a_successful_login_resets_the_count_of_failures()
    {
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);

        await FailLoginsAsync(email, 4);
        await LoginAsync(email, Password, HttpStatusCode.OK);
        await FailLoginsAsync(email, 4);

        await LoginAsync(email, Password, HttpStatusCode.OK);
    }

    [Fact]
    public async Task a_locked_out_account_cannot_refresh_its_tokens()
    {
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        var tokens = (await LoginAsync(email, Password, HttpStatusCode.OK)).ReadAsJson<LoginResponse>()!;

        await FailLoginsAsync(email, 4);
        await LoginAsync(email, "not-the-password", HttpStatusCode.Forbidden);

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(tokens.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    [Fact]
    public async Task resetting_the_password_lifts_the_lockout()
    {
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        await FailLoginsAsync(email, 4);
        await LoginAsync(email, "not-the-password", HttpStatusCode.Forbidden);

        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ForgotPasswordRequest(email)).ToUrl("/api/auth/forgot-password");
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));
        var query = _fixture.Emails.LinkQuery(email, "/reset-password");
        const string newPassword = "N3w-P@ssword!";
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ResetPasswordRequest(email, query["token"]!, newPassword)).ToUrl("/api/auth/reset-password");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        await LoginAsync(email, newPassword, HttpStatusCode.OK);
    }

    [Fact]
    public async Task wrong_passwords_for_an_unconfirmed_account_count_towards_a_lockout()
    {
        var email = NewEmail();
        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RegisterRequest(email, Password)).ToUrl("/api/auth/register");
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        }));

        await FailLoginsAsync(email, 5);

        // Locked: the right password no longer reveals that only confirmation is missing.
        var refused = await LoginAsync(email, Password, HttpStatusCode.Forbidden);
        Assert.Contains("LockedOut", await refused.ReadAsTextAsync(), StringComparison.Ordinal);
    }
}
