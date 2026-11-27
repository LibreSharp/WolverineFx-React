using System.Net;
using Alba;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Identity.Endpoints;
#if (IsMarten)
using ApiHost.Identity.Marten;
using Marten;
#elif (IsEfCore)
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// Identity only (excluded otherwise by template.json): profile, password and account deletion.
[Collection("api")]
public class AccountEndpointsTests
{
    private const string Password = "P@ssword123!";
    private const string NewPassword = "N3w-P@ssword!";

    private readonly AppFixture _fixture;

    public AccountEndpointsTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private async Task<LoginResponse> RegisterAndLoginAsync(string email)
    {
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        return (await LoginAsync(email, Password, HttpStatusCode.OK))!;
    }

    private async Task<LoginResponse?> LoginAsync(string email, string password, HttpStatusCode expected)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBe(expected);
        });
        return expected == HttpStatusCode.OK ? result.ReadAsJson<LoginResponse>() : null;
    }

    private Task RefreshAsync(string refreshToken, HttpStatusCode expected) =>
        _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(refreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBe(expected);
        });

    private async Task<MeResponse> MeAsync(string accessToken) =>
        (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/me");
            s.WithBearerToken(accessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<MeResponse>()!;

    [Fact]
    public async Task updating_the_profile_changes_the_display_name()
    {
        var email = NewEmail();
        var tokens = await RegisterAndLoginAsync(email);

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new UpdateProfileRequest(email, "  Ada Lovelace ")).ToUrl("/api/account/profile");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBeOk();
        });

        Assert.Null(result.ReadAsJson<UpdateProfileResponse>()!.ConfirmationSentTo);
        Assert.Equal("Ada Lovelace", (await MeAsync(tokens.AccessToken)).DisplayName);
    }

    [Fact]
    public async Task a_new_email_is_used_only_after_the_link_sent_to_it_is_opened()
    {
        var email = NewEmail();
        var tokens = await RegisterAndLoginAsync(email);

        var newEmail = NewEmail();
        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new UpdateProfileRequest(newEmail, null)).ToUrl("/api/account/profile");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBeOk();
        }));

        // Nothing changes until the new address is confirmed.
        Assert.Equal(email, (await MeAsync(tokens.AccessToken)).Email);
        await LoginAsync(email, Password, HttpStatusCode.OK);

        var query = _fixture.Emails.LinkQuery(newEmail, "/confirm-email-change");
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailChangeRequest(query["userId"]!, query["email"]!, query["token"]!)).ToUrl("/api/auth/confirm-email-change");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        Assert.Equal(newEmail, (await MeAsync(tokens.AccessToken)).Email);
        await LoginAsync(newEmail, Password, HttpStatusCode.OK);
        await LoginAsync(email, Password, HttpStatusCode.Unauthorized);

        // The link works once.
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ConfirmEmailChangeRequest(query["userId"]!, query["email"]!, query["token"]!)).ToUrl("/api/auth/confirm-email-change");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task an_email_that_belongs_to_another_account_is_rejected()
    {
        var otherEmail = NewEmail();
        await RegisterAndLoginAsync(otherEmail);
        var tokens = await RegisterAndLoginAsync(NewEmail());

        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new UpdateProfileRequest(otherEmail, null)).ToUrl("/api/account/profile");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task changing_the_password_signs_out_other_sessions_but_keeps_this_one()
    {
        var email = NewEmail();
        var thisSession = await RegisterAndLoginAsync(email);
        var otherSession = (await LoginAsync(email, Password, HttpStatusCode.OK))!;

        var result = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ChangePasswordRequest(Password, NewPassword)).ToUrl("/api/account/password");
            s.WithBearerToken(thisSession.AccessToken);
            s.StatusCodeShouldBeOk();
        });
        var newTokens = result.ReadAsJson<LoginResponse>()!;

        await LoginAsync(email, Password, HttpStatusCode.Unauthorized);
        await LoginAsync(email, NewPassword, HttpStatusCode.OK);
        await RefreshAsync(otherSession.RefreshToken, HttpStatusCode.Unauthorized);
        await RefreshAsync(newTokens.RefreshToken, HttpStatusCode.OK);
    }

    [Fact]
    public async Task changing_the_password_needs_the_current_password()
    {
        var email = NewEmail();
        var tokens = await RegisterAndLoginAsync(email);

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ChangePasswordRequest("wrong-password", NewPassword)).ToUrl("/api/account/password");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        await LoginAsync(email, Password, HttpStatusCode.OK);
    }

    [Fact]
    public async Task deleting_the_account_needs_the_password()
    {
        var email = NewEmail();
        var tokens = await RegisterAndLoginAsync(email);

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new DeleteAccountRequest("wrong-password")).ToUrl("/api/account/delete");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        await LoginAsync(email, Password, HttpStatusCode.OK);
    }

    [Fact]
    public async Task deleting_the_account_removes_the_user_and_their_todos()
    {
        var email = NewEmail();
        var tokens = await RegisterAndLoginAsync(email);
        var userId = (await MeAsync(tokens.AccessToken)).UserId;
        var created = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo("Personal data")).ToUrl("/api/todos");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });
        var todoId = created.ReadAsJson<TodoCreationResponse>()!.Id;

        // Waits for the AccountDeleted message to be handled.
        var tracked = await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new DeleteAccountRequest(Password)).ToUrl("/api/account/delete");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        }));
        Assert.Contains(tracked.Executed.MessagesOf<AccountDeleted>(), m => m.UserId == userId);

        await LoginAsync(email, Password, HttpStatusCode.Unauthorized);
        await RefreshAsync(tokens.RefreshToken, HttpStatusCode.Unauthorized);

        using var scope = _fixture.Host.Services.CreateScope();
#if (IsMarten)
        var session = scope.ServiceProvider.GetRequiredService<IQuerySession>();
        Assert.Equal(0, await session.Query<Todo>().CountAsync(t => t.OwnerId == userId));
        // The event stream is permanently deleted too, not just the snapshot document.
        Assert.Empty(await session.Events.FetchStreamAsync(todoId));
#elif (IsEfCore)
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Todos.CountAsync(t => t.OwnerId == userId));
        Assert.Null(await db.Todos.FindAsync(todoId));
#endif
    }

    [Fact]
    public async Task a_password_reset_link_sets_a_new_password_and_signs_out_every_session()
    {
        var email = NewEmail();
        var session = await RegisterAndLoginAsync(email);

        // Waits for the email, which is sent in the background.
        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ForgotPasswordRequest(email)).ToUrl("/api/auth/forgot-password");
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));

        // The emailed link points at the client's /reset-password page with the email and token.
        var query = _fixture.Emails.LinkQuery(email, "/reset-password");

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ResetPasswordRequest(query["email"]!, query["token"]!, NewPassword)).ToUrl("/api/auth/reset-password");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });

        await LoginAsync(email, NewPassword, HttpStatusCode.OK);
        await LoginAsync(email, Password, HttpStatusCode.Unauthorized);
        await RefreshAsync(session.RefreshToken, HttpStatusCode.Unauthorized);

        // The link works once.
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ResetPasswordRequest(query["email"]!, query["token"]!, "An0ther-P@ssword!")).ToUrl("/api/auth/reset-password");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task forgot_password_does_not_reveal_whether_an_email_is_registered()
    {
        var unknownEmail = NewEmail();

        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ForgotPasswordRequest(unknownEmail)).ToUrl("/api/auth/forgot-password");
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));

        Assert.Empty(_fixture.Emails.SentTo(unknownEmail));
    }

    [Fact]
    public async Task an_invalid_reset_token_is_rejected()
    {
        var email = NewEmail();
        await RegisterAndLoginAsync(email);

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ResetPasswordRequest(email, "not-a-real-token", NewPassword)).ToUrl("/api/auth/reset-password");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        await LoginAsync(email, Password, HttpStatusCode.OK);
    }

    [Fact]
    public async Task data_protection_keys_are_kept_in_the_database()
    {
        // Creating a reset token uses (and if needed creates) a Data Protection key.
        var email = NewEmail();
        await RegisterAndLoginAsync(email);
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ForgotPasswordRequest(email)).ToUrl("/api/auth/forgot-password");
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        });

        // Stored in PostgreSQL, so reset links survive restarts and work on every instance.
        using var scope = _fixture.Host.Services.CreateScope();
#if (IsMarten)
        var session = scope.ServiceProvider.GetRequiredService<IQuerySession>();
        Assert.True(await session.Query<DataProtectionKeyDocument>().AnyAsync());
#elif (IsEfCore)
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.DataProtectionKeys.AnyAsync());
#endif
    }

    [Fact]
    public async Task an_invalid_email_or_overlong_display_name_is_rejected()
    {
        var email = NewEmail();
        var tokens = await RegisterAndLoginAsync(email);

        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new UpdateProfileRequest("not-an-email", null)).ToUrl("/api/account/profile");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new UpdateProfileRequest(email, new string('x', 101))).ToUrl("/api/account/profile");
            s.WithBearerToken(tokens.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }
}
