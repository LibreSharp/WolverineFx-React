using System.Net;
using Alba;
using ApiHost.Identity;
using ApiHost.Identity.Admin;
using ApiHost.Identity.Endpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// Identity only (excluded otherwise by template.json): roles, the admin endpoints and invitations.
[Collection("api")]
public class AdminEndpointsTests
{
    private const string Password = "P@ssword123!";

    private readonly AppFixture _fixture;

    public AdminEndpointsTests(AppFixture fixture)
    {
        _fixture = fixture;
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private async Task<IScenarioResult> LoginAsync(string email, string password, HttpStatusCode expected) =>
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBe(expected);
        });

    private async Task<LoginResponse> LoginAsync(string email, string password) =>
        (await LoginAsync(email, password, HttpStatusCode.OK)).ReadAsJson<LoginResponse>()!;

    // The development user is an admin (Accounts:AdminEmails in appsettings.Development.json).
    private Task<LoginResponse> LoginAsAdminAsync()
    {
        var configuration = _fixture.Host.Services.GetRequiredService<IConfiguration>();
        return LoginAsync(configuration["DevelopmentUser:Email"]!, configuration["DevelopmentUser:Password"]!);
    }

    private async Task<AdminUser> FindAsync(string adminToken, string email)
    {
        var result = await _fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/admin/users?search={Uri.EscapeDataString(email)}");
            s.WithBearerToken(adminToken);
            s.StatusCodeShouldBeOk();
        });
        return Assert.Single(result.ReadAsJson<AdminUserList>()!.Users);
    }

    private Task<IScenarioResult> AdminPostAsync(string adminToken, string url, HttpStatusCode expected) =>
        _fixture.Host.Scenario(s =>
        {
            s.Post.Url(url);
            s.WithBearerToken(adminToken);
            s.StatusCodeShouldBe(expected);
        });

    [Fact]
    public async Task only_admins_can_use_the_admin_endpoints()
    {
        await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/admin/users");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });

        var userToken = await _fixture.CreateUserAndGetAccessTokenAsync();
        await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/admin/users");
            s.WithBearerToken(userToken);
            s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });
    }

    [Fact]
    public async Task admins_see_their_role_and_can_search_users()
    {
        var admin = await LoginAsAdminAsync();
        var me = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auth/me");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<MeResponse>()!;
        Assert.Contains(AppRoles.Admin, me.Roles, StringComparer.Ordinal);

        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);

        var user = await FindAsync(admin.AccessToken, email[..12].ToUpperInvariant());
        Assert.Equal(email, user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.Empty(user.Roles);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public async Task a_locked_account_cannot_log_in_or_refresh_until_unlocked()
    {
        var admin = await LoginAsAdminAsync();
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        var session = await LoginAsync(email, Password);
        var user = await FindAsync(admin.AccessToken, email);

        await AdminPostAsync(admin.AccessToken, $"/api/admin/users/{user.Id}/lock", HttpStatusCode.NoContent);

        Assert.True((await FindAsync(admin.AccessToken, email)).LockedByAdmin);
        var refused = await LoginAsync(email, Password, HttpStatusCode.Forbidden);
        Assert.Contains("LockedOut", await refused.ReadAsTextAsync(), StringComparison.Ordinal);
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(session.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });

        // A password reset doesn't lift an admin's lock.
        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ForgotPasswordRequest(email)).ToUrl("/api/auth/forgot-password");
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));
        var query = _fixture.Emails.LinkQuery(email, "/reset-password");
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ResetPasswordRequest(email, query["token"]!, Password + "2")).ToUrl("/api/auth/reset-password");
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await LoginAsync(email, Password + "2", HttpStatusCode.Forbidden);

        await AdminPostAsync(admin.AccessToken, $"/api/admin/users/{user.Id}/unlock", HttpStatusCode.NoContent);
        await LoginAsync(email, Password + "2", HttpStatusCode.OK);
    }

    [Fact]
    public async Task admins_cannot_lock_themselves_or_remove_their_own_admin_role()
    {
        var admin = await LoginAsAdminAsync();
        var configuration = _fixture.Host.Services.GetRequiredService<IConfiguration>();
        var self = await FindAsync(admin.AccessToken, configuration["DevelopmentUser:Email"]!);

        await AdminPostAsync(admin.AccessToken, $"/api/admin/users/{self.Id}/lock", HttpStatusCode.BadRequest);
        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new SetRolesRequest([])).ToUrl($"/api/admin/users/{self.Id}/roles");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task roles_given_by_an_admin_apply_from_the_next_login()
    {
        var admin = await LoginAsAdminAsync();
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        var user = await FindAsync(admin.AccessToken, email);

        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new SetRolesRequest([AppRoles.Admin])).ToUrl($"/api/admin/users/{user.Id}/roles");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        Assert.Equal([AppRoles.Admin], (await FindAsync(admin.AccessToken, email)).Roles);

        var promoted = await LoginAsync(email, Password);
        await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/admin/users");
            s.WithBearerToken(promoted.AccessToken);
            s.StatusCodeShouldBeOk();
        });

        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new SetRolesRequest([])).ToUrl($"/api/admin/users/{user.Id}/roles");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        Assert.Empty((await FindAsync(admin.AccessToken, email)).Roles);
    }

    [Fact]
    public async Task unknown_roles_are_rejected()
    {
        var admin = await LoginAsAdminAsync();
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        var user = await FindAsync(admin.AccessToken, email);

        await _fixture.Host.Scenario(s =>
        {
            s.Put.Json(new SetRolesRequest(["Superhero"])).ToUrl($"/api/admin/users/{user.Id}/roles");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task an_invited_person_chooses_a_password_from_the_emailed_link_and_is_signed_in()
    {
        var admin = await LoginAsAdminAsync();
        var email = NewEmail();

        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new InviteUserRequest(email, [AppRoles.Admin])).ToUrl("/api/admin/invitations");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));

        var invited = await FindAsync(admin.AccessToken, email);
        Assert.True(invited.InvitationPending);
        Assert.Equal([AppRoles.Admin], invited.Roles);

        var query = _fixture.Emails.LinkQuery(email, "/accept-invitation");
        var accepted = await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new AcceptInvitationRequest(query["userId"]!, query["token"]!, Password)).ToUrl("/api/auth/accept-invitation");
            s.StatusCodeShouldBeOk();
        });
        Assert.False(string.IsNullOrEmpty(accepted.ReadAsJson<LoginResponse>()!.AccessToken));

        var user = await FindAsync(admin.AccessToken, email);
        Assert.False(user.InvitationPending);
        Assert.True(user.EmailConfirmed);
        await LoginAsync(email, Password, HttpStatusCode.OK);

        // The link works once.
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new AcceptInvitationRequest(query["userId"]!, query["token"]!, "An0ther-P@ssword!")).ToUrl("/api/auth/accept-invitation");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task inviting_a_pending_invitee_again_resends_but_an_existing_account_is_rejected()
    {
        var admin = await LoginAsAdminAsync();
        var email = NewEmail();
        for (var i = 0; i < 2; i++)
        {
            await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
            {
                s.Post.Json(new InviteUserRequest(email, null)).ToUrl("/api/admin/invitations");
                s.WithBearerToken(admin.AccessToken);
                s.StatusCodeShouldBe(HttpStatusCode.Accepted);
            }));
        }

        Assert.Equal(2, _fixture.Emails.SentTo(email).Count);

        var existing = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(existing, Password);
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new InviteUserRequest(existing, null)).ToUrl("/api/admin/invitations");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
    }

    [Fact]
    public async Task with_registration_closed_only_invited_people_get_accounts()
    {
        var configuration = _fixture.Host.Services.GetRequiredService<IConfiguration>();
        configuration["Accounts:AllowRegistration"] = "false";
        try
        {
            var options = await _fixture.Host.Scenario(s =>
            {
                s.Get.Url("/api/auth/options");
                s.StatusCodeShouldBeOk();
            });
            Assert.False(options.ReadAsJson<AuthOptionsResponse>()!.AllowRegistration);

            await _fixture.Host.Scenario(s =>
            {
                s.Post.Json(new RegisterRequest(NewEmail(), Password)).ToUrl("/api/auth/register");
                s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
            });

            var admin = await LoginAsAdminAsync();
            await _fixture.Host.Scenario(s =>
            {
                s.Post.Json(new InviteUserRequest(NewEmail(), null)).ToUrl("/api/admin/invitations");
                s.WithBearerToken(admin.AccessToken);
                s.StatusCodeShouldBe(HttpStatusCode.Accepted);
            });
        }
        finally
        {
            configuration["Accounts:AllowRegistration"] = "true";
        }
    }

    [Fact]
    public async Task an_email_in_admin_emails_becomes_an_admin_only_once_confirmed()
    {
        var configuration = _fixture.Host.Services.GetRequiredService<IConfiguration>();
        var email = NewEmail();
        configuration["Accounts:AdminEmails:9"] = email;
        try
        {
            // Registered but not confirmed: no role.
            await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
            {
                s.Post.Json(new RegisterRequest(email, Password)).ToUrl("/api/auth/register");
                s.StatusCodeShouldBe(HttpStatusCode.Created);
            }));
            var admin = await LoginAsAdminAsync();
            Assert.Empty((await FindAsync(admin.AccessToken, email)).Roles);

            // Confirmed and logged in: Admin.
            var query = _fixture.Emails.LinkQuery(email, "/confirm-email");
            await _fixture.Host.Scenario(s =>
            {
                s.Post.Json(new ConfirmEmailRequest(query["userId"]!, query["token"]!)).ToUrl("/api/auth/confirm-email");
                s.StatusCodeShouldBe(HttpStatusCode.NoContent);
            });
            var login = await LoginAsync(email, Password);
            await _fixture.Host.Scenario(s =>
            {
                s.Get.Url("/api/admin/users");
                s.WithBearerToken(login.AccessToken);
                s.StatusCodeShouldBeOk();
            });
        }
        finally
        {
            configuration["Accounts:AdminEmails:9"] = string.Empty;
        }
    }

    [Fact]
    public async Task an_invitation_needs_a_password_that_meets_the_rules()
    {
        var admin = await LoginAsAdminAsync();
        var email = NewEmail();
        await _fixture.Host.ExecuteAndWaitAsync(() => _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new InviteUserRequest(email, null)).ToUrl("/api/admin/invitations");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));
        var query = _fixture.Emails.LinkQuery(email, "/accept-invitation");

        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new AcceptInvitationRequest(query["userId"]!, query["token"]!, "weak")).ToUrl("/api/auth/accept-invitation");
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        // Still pending, and the link still works with a good password.
        Assert.True((await FindAsync(admin.AccessToken, email)).InvitationPending);
        await _fixture.Host.Scenario(s =>
        {
            s.Post.Json(new AcceptInvitationRequest(query["userId"]!, query["token"]!, Password)).ToUrl("/api/auth/accept-invitation");
            s.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task unlocking_also_lifts_a_lockout_from_failed_logins()
    {
        var admin = await LoginAsAdminAsync();
        var email = NewEmail();
        await _fixture.RegisterConfirmedUserAsync(email, Password);
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(email, "not-the-password", i < 4 ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
        }

        var user = await FindAsync(admin.AccessToken, email);
        Assert.NotNull(user.LockedUntil);
        Assert.False(user.LockedByAdmin);

        await AdminPostAsync(admin.AccessToken, $"/api/admin/users/{user.Id}/unlock", HttpStatusCode.NoContent);
        await LoginAsync(email, Password, HttpStatusCode.OK);
    }

    [Fact]
    public async Task the_user_list_is_paged()
    {
        var admin = await LoginAsAdminAsync();
        var first = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/admin/users?page=1");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<AdminUserList>()!;
        Assert.Equal(ListUsersEndpoint.PageSize, first.PageSize);
        Assert.True(first.Users.Count <= first.PageSize);
        Assert.Equal(AppRoles.All, first.AllRoles);

        var beyond = (await _fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/admin/users?page={(first.Total / first.PageSize) + 2}");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<AdminUserList>()!;
        Assert.Empty(beyond.Users);
        Assert.Equal(first.Total, beyond.Total);
    }
}
