using System.Net;
using Alba;
using ApiHost.Features.Account;
using ApiHost.Features.Auth;
using ApiHost.Features.Organisations;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Organisations;
#if (IsMarten)
using Marten;
#else
using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// --Auth saas only (excluded otherwise by template.json): organisations, switching between them,
// members' roles, and keeping each organisation's data apart.
[Collection("api")]
public class OrganisationTests(AppFixture fixture)
{
    private const string Password = "P@ssword123!";

    private sealed record Session(string UserId, string Email, LoginResponse Tokens)
    {
        public string AccessToken => Tokens.AccessToken;

        public Guid OrganisationId => Guid.Parse(new JsonWebToken(AccessToken).GetClaim(OrganisationClaims.OrganisationId).Value);
    }

    private async Task<Session> SignUpAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await fixture.RegisterConfirmedUserAsync(email, Password);
        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, Password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        return new Session(new JsonWebToken(tokens.AccessToken).Subject, email, tokens);
    }

    private async Task<Guid> CreateOrganisationAsync(Session owner, string name)
        => (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrganisationRequest(name)).ToUrl("/api/organisations");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        })).ReadAsJson<OrganisationCreationResponse>()!.Id;

    private async Task<Session> SwitchAsync(Session session, Guid organisationId)
    {
        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SwitchOrganisationRequest(organisationId, session.Tokens.RefreshToken)).ToUrl("/api/organisations/switch");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        return session with { Tokens = tokens };
    }

    // Until invitations: add a member straight to the store.
    private async Task AddMemberAsync(Guid organisationId, Session member, string role)
    {
        using var scope = fixture.Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IOrganisationStore>().AddMembershipAsync(new Membership
        {
            Id = Guid.CreateVersion7(),
            OrganisationId = organisationId,
            UserId = member.UserId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow,
        }, CancellationToken.None);
    }

    private async Task<List<Todo>> TodosAsync(Session session)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<List<Todo>>()!;

    private async Task CreateTodoAsync(Session session, string title)
        => await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });

    private Task<IScenarioResult> ChangeRoleAsync(Session actor, string userId, string role, HttpStatusCode expected)
        => fixture.Host.Scenario(s =>
        {
            s.Put.Json(new ChangeMemberRoleRequest(role)).ToUrl($"/api/organisation/members/{userId}/role");
            s.WithBearerToken(actor.AccessToken);
            s.StatusCodeShouldBe(expected);
        });

    private Task<IScenarioResult> RemoveAsync(Session actor, string userId, HttpStatusCode expected)
        => fixture.Host.Scenario(s =>
        {
            s.Delete.Url($"/api/organisation/members/{userId}");
            s.WithBearerToken(actor.AccessToken);
            s.StatusCodeShouldBe(expected);
        });

    [Fact]
    public async Task a_new_account_works_in_its_own_organisation_as_its_owner()
    {
        var session = await SignUpAsync();

        var organisations = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisations");
            s.WithBearerToken(session.AccessToken);
        })).ReadAsJson<List<UserOrganisation>>()!;
        var own = Assert.Single(organisations);
        Assert.True(own.IsCurrent);
        Assert.Equal(OrganisationRoles.Owner, own.Role);
        Assert.Equal(session.OrganisationId, own.Id);
    }

    [Fact]
    public async Task members_share_an_organisations_todos_and_nobody_else_sees_them()
    {
        var owner = await SignUpAsync();
        var member = await SignUpAsync();
        var outsider = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Team");
        await AddMemberAsync(team, member, OrganisationRoles.Member);

        owner = await SwitchAsync(owner, team);
        var title = $"Shared {Guid.NewGuid()}";
        await CreateTodoAsync(owner, title);

        member = await SwitchAsync(member, team);
        Assert.Contains(await TodosAsync(member), t => t.Title == title);
        Assert.DoesNotContain(await TodosAsync(outsider), t => t.Title == title);

        // The owner's own organisation doesn't have it either.
        var personal = await SwitchAsync(owner, (await SignInOrganisationsAsync(owner)).Single(o => o.Id != team).Id);
        Assert.DoesNotContain(await TodosAsync(personal), t => t.Title == title);
    }

    private async Task<List<UserOrganisation>> SignInOrganisationsAsync(Session session)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisations");
            s.WithBearerToken(session.AccessToken);
        })).ReadAsJson<List<UserOrganisation>>()!;

    [Fact]
    public async Task only_members_can_switch_to_an_organisation_and_refreshing_keeps_it()
    {
        var owner = await SignUpAsync();
        var stranger = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Private");

        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SwitchOrganisationRequest(team, stranger.Tokens.RefreshToken)).ToUrl("/api/organisations/switch");
            s.WithBearerToken(stranger.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });
        // Another user's refresh token can't be used to switch.
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SwitchOrganisationRequest(team, stranger.Tokens.RefreshToken)).ToUrl("/api/organisations/switch");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });

        owner = await SwitchAsync(owner, team);
        var refreshed = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(owner.Tokens.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<RefreshResponse>()!;
        Assert.Equal(team, Guid.Parse(new JsonWebToken(refreshed.AccessToken).GetClaim(OrganisationClaims.OrganisationId).Value));

        // A new session starts in the organisation used last.
        var again = await SignInOrganisationsAsync(owner);
        Assert.True(again.Single(o => o.Id == team).IsCurrent);
    }

    [Fact]
    public async Task members_cannot_manage_others_and_admins_cannot_manage_owners()
    {
        var owner = await SignUpAsync();
        var admin = await SignUpAsync();
        var member = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Roles");
        await AddMemberAsync(team, admin, OrganisationRoles.Admin);
        await AddMemberAsync(team, member, OrganisationRoles.Member);
        owner = await SwitchAsync(owner, team);
        admin = await SwitchAsync(admin, team);
        member = await SwitchAsync(member, team);

        await ChangeRoleAsync(member, admin.UserId, OrganisationRoles.Member, HttpStatusCode.BadRequest);
        await RemoveAsync(member, admin.UserId, HttpStatusCode.BadRequest);
        await ChangeRoleAsync(admin, member.UserId, OrganisationRoles.Owner, HttpStatusCode.BadRequest);
        await ChangeRoleAsync(admin, owner.UserId, OrganisationRoles.Member, HttpStatusCode.BadRequest);
        await RemoveAsync(admin, owner.UserId, HttpStatusCode.BadRequest);

        await ChangeRoleAsync(admin, member.UserId, OrganisationRoles.Admin, HttpStatusCode.NoContent);
        await ChangeRoleAsync(owner, admin.UserId, OrganisationRoles.Owner, HttpStatusCode.NoContent);

        var members = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation/members");
            s.WithBearerToken(member.AccessToken);
        })).ReadAsJson<List<OrganisationMember>>()!;
        Assert.Equal(2, members.Count(m => m.Role == OrganisationRoles.Owner));
    }

    [Fact]
    public async Task the_last_owner_cannot_leave_or_step_down()
    {
        var owner = await SignUpAsync();
        var member = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Last owner");
        await AddMemberAsync(team, member, OrganisationRoles.Member);
        owner = await SwitchAsync(owner, team);

        await RemoveAsync(owner, owner.UserId, HttpStatusCode.BadRequest);
        await ChangeRoleAsync(owner, owner.UserId, OrganisationRoles.Admin, HttpStatusCode.BadRequest);

        // A member can leave.
        member = await SwitchAsync(member, team);
        await RemoveAsync(member, member.UserId, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task a_removed_member_loses_access_at_once()
    {
        var owner = await SignUpAsync();
        var member = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Removal");
        await AddMemberAsync(team, member, OrganisationRoles.Member);
        owner = await SwitchAsync(owner, team);
        member = await SwitchAsync(member, team);

        await RemoveAsync(owner, member.UserId, HttpStatusCode.NoContent);

        // The access token still names the organisation, but isn't accepted any more...
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.WithBearerToken(member.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
        // ...and refreshing moves the session to one of the user's remaining organisations.
        var refreshed = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(member.Tokens.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<RefreshResponse>()!;
        Assert.NotEqual(team, Guid.Parse(new JsonWebToken(refreshed.AccessToken).GetClaim(OrganisationClaims.OrganisationId).Value));
    }

    [Fact]
    public async Task only_an_owner_can_rename_or_delete_and_deleting_removes_the_data()
    {
        var owner = await SignUpAsync();
        var admin = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Doomed");
        await AddMemberAsync(team, admin, OrganisationRoles.Admin);
        owner = await SwitchAsync(owner, team);
        admin = await SwitchAsync(admin, team);
        await CreateTodoAsync(owner, "Goes with the organisation");

        await fixture.Host.Scenario(s =>
        {
            s.Put.Json(new RenameOrganisationRequest("Renamed")).ToUrl("/api/organisation");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });
        await fixture.Host.Scenario(s =>
        {
            s.Put.Json(new RenameOrganisationRequest("Renamed")).ToUrl("/api/organisation");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await fixture.Host.Scenario(s =>
        {
            s.Delete.Url("/api/organisation");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });

        await fixture.Host.ExecuteAndWaitAsync(() => fixture.Host.Scenario(s =>
        {
            s.Delete.Url("/api/organisation");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        }));

        Assert.Equal(0, await CountTodosAsync(team));
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    private async Task<int> CountTodosAsync(Guid organisationId)
    {
        using var scope = fixture.Host.Services.CreateScope();
#if (IsMarten)
        await using var session = scope.ServiceProvider.GetRequiredService<IDocumentStore>().QuerySession(organisationId.ToString());
        return await session.Query<Todo>().CountAsync();
#else
        var tenant = organisationId.ToString();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Todos.IgnoreQueryFilters().CountAsync(t => t.TenantId == tenant);
#endif
    }

    // Invites by email and returns the token from the emailed link.
    private async Task<string> InviteAsync(Session actor, string email, string role)
    {
        await fixture.Host.ExecuteAndWaitAsync(() => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new InviteMemberRequest(email, role)).ToUrl("/api/organisation/invitations");
            s.WithBearerToken(actor.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));
        return fixture.Emails.LinkQuery(email, "/join")["token"]!;
    }

    private Task<IScenarioResult> AcceptAsync(Session session, string token, HttpStatusCode expected)
        => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new JoinOrganisationRequest(token)).ToUrl("/api/join");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBe(expected);
        });

    [Fact]
    public async Task an_invited_user_joins_with_the_invited_role_and_the_link_works_once()
    {
        var owner = await SignUpAsync();
        var invitee = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Invites");
        owner = await SwitchAsync(owner, team);

        var token = await InviteAsync(owner, invitee.Email, OrganisationRoles.Admin);
        var details = (await fixture.Host.Scenario(s => s.Get.Url($"/api/join/{token}"))).ReadAsJson<InvitationDetails>()!;
        Assert.Equal("Invites", details.OrganisationName);
        Assert.True(details.AccountExists);

        await AcceptAsync(invitee, token, HttpStatusCode.OK);
        invitee = await SwitchAsync(invitee, team);
        var current = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation");
            s.WithBearerToken(invitee.AccessToken);
        })).ReadAsJson<CurrentOrganisation>()!;
        Assert.Equal(OrganisationRoles.Admin, current.Role);

        await AcceptAsync(invitee, token, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task someone_new_creates_a_confirmed_account_from_the_invitation()
    {
        var owner = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Newcomers");
        owner = await SwitchAsync(owner, team);
        var email = $"{Guid.NewGuid():N}@example.com";

        var token = await InviteAsync(owner, email, OrganisationRoles.Member);
        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new JoinWithNewAccountRequest(token, Password)).ToUrl("/api/join/register");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        Assert.Equal(team, Guid.Parse(new JsonWebToken(tokens.AccessToken).GetClaim(OrganisationClaims.OrganisationId).Value));

        // The link proved the email, so the account can log in at once.
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, Password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task an_invitation_is_only_for_its_email_and_stops_working_when_revoked()
    {
        var owner = await SignUpAsync();
        var other = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Guarded");
        owner = await SwitchAsync(owner, team);

        var token = await InviteAsync(owner, $"{Guid.NewGuid():N}@example.com", OrganisationRoles.Member);
        await AcceptAsync(other, token, HttpStatusCode.BadRequest);

        var invitation = Assert.Single((await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation/invitations");
            s.WithBearerToken(owner.AccessToken);
        })).ReadAsJson<List<PendingInvitation>>()!);
        await fixture.Host.Scenario(s =>
        {
            s.Delete.Url($"/api/organisation/invitations/{invitation.Id}");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/join/{token}");
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });
    }

    [Fact]
    public async Task members_cannot_invite_and_admins_cannot_invite_owners()
    {
        var owner = await SignUpAsync();
        var admin = await SignUpAsync();
        var member = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Who invites");
        await AddMemberAsync(team, admin, OrganisationRoles.Admin);
        await AddMemberAsync(team, member, OrganisationRoles.Member);
        admin = await SwitchAsync(admin, team);
        member = await SwitchAsync(member, team);

        foreach (var (actor, role) in new[] { (member, OrganisationRoles.Member), (admin, OrganisationRoles.Owner) })
        {
            await fixture.Host.Scenario(s =>
            {
                s.Post.Json(new InviteMemberRequest($"{Guid.NewGuid():N}@example.com", role)).ToUrl("/api/organisation/invitations");
                s.WithBearerToken(actor.AccessToken);
                s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
            });
        }
    }

    [Fact]
    public async Task an_account_cannot_be_deleted_while_it_is_the_only_owner_of_a_team()
    {
        var owner = await SignUpAsync();
        var member = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Needs an owner");
        await AddMemberAsync(team, member, OrganisationRoles.Member);

        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new DeleteAccountRequest(Password)).ToUrl("/api/account/delete");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        owner = await SwitchAsync(owner, team);
        await ChangeRoleAsync(owner, member.UserId, OrganisationRoles.Owner, HttpStatusCode.NoContent);
        await fixture.Host.ExecuteAndWaitAsync(() => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new DeleteAccountRequest(Password)).ToUrl("/api/account/delete");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        }));

        // The team carries on with its other owner; the deleted user's own organisation is gone.
        member = await SwitchAsync(member, team);
        var members = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation/members");
            s.WithBearerToken(member.AccessToken);
        })).ReadAsJson<List<OrganisationMember>>()!;
        Assert.Equal(member.UserId, Assert.Single(members).UserId);
    }
}
