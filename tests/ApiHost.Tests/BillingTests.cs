using System.Net;
using System.Text.Json.Nodes;
using Alba;
using ApiHost.Billing;
using ApiHost.Features.Auth;
using ApiHost.Features.Billing;
using ApiHost.Features.Organisations;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Organisations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// --Auth saas only (excluded otherwise by template.json): plans, the reverse trial, features and
// limits, and what a downgrade does.
[Collection("api")]
public class BillingTests(AppFixture fixture)
{
    private const string Password = "P@ssword123!";

    private sealed record Session(string Email, string AccessToken)
    {
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
        return new Session(email, tokens.AccessToken);
    }

    private async Task<BillingSummary> BillingAsync(Session session)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/billing");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<BillingSummary>()!;

    // Puts the organisation on a plan, as the payment provider's webhooks would.
    private async Task SetAccountAsync(Guid organisationId, string status, string planId = PlanCatalogue.Free, int version = 1)
    {
        using var scope = fixture.Host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBillingStore>();
        var account = await store.FindAccountAsync(organisationId, CancellationToken.None) ?? new BillingAccount { Id = organisationId };
        account.PlanId = planId;
        account.PlanVersion = version;
        account.Status = status;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveAccountAsync(account, CancellationToken.None);
    }

    private Task<IScenarioResult> CreateTodoAsync(Session session, string title, HttpStatusCode expected)
        => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBe(expected);
        });

    private Task<IScenarioResult> RemindAsync(Session session, Guid todoId, HttpStatusCode expected)
        => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RemindTodo(60)).ToUrl($"/api/todos/{todoId}/remind");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBe(expected);
        });

    private static LimitUsage Limit(BillingSummary billing, string name) => billing.Limits.Single(l => l.Name == name);

    [Fact]
    public async Task a_new_organisation_starts_on_a_trial_of_the_top_plan()
    {
        var owner = await SignUpAsync();

        var billing = await BillingAsync(owner);

        Assert.Equal(PlanCatalogue.Pro, billing.PlanId);
        Assert.Equal(BillingStatuses.Trial, billing.Status);
        Assert.NotNull(billing.TrialEndsAt);
        Assert.InRange(billing.TrialEndsAt.Value - DateTimeOffset.UtcNow, TimeSpan.FromDays(13.9), TimeSpan.FromDays(14));
        Assert.Contains(PlanFeatures.TodoReminders, billing.Features, StringComparer.Ordinal);
        Assert.Null(Limit(billing, PlanLimits.OpenTodos).Limit);
        Assert.Equal(1, Limit(billing, PlanLimits.Members).Used);
        Assert.True(billing.CanManage);
    }

    [Fact]
    public async Task after_a_downgrade_data_over_a_limit_stays_but_nothing_more_can_be_added()
    {
        var owner = await SignUpAsync();
        var free = PlanCatalogue.Latest(PlanCatalogue.Free);
        var openLimit = free.LimitOf(PlanLimits.OpenTodos)!.Value;
        var ids = new List<Guid>();
        for (var i = 0; i < openLimit + 1; i++)
        {
            ids.Add((await CreateTodoAsync(owner, $"Todo {i}", HttpStatusCode.Created)).ReadAsJson<TodoCreationResponse>()!.Id);
        }

        // The trial ends without a subscription.
        await SetAccountAsync(owner.OrganisationId, SubscriptionStatuses.None);

        var billing = await BillingAsync(owner);
        Assert.Equal(BillingStatuses.Free, billing.Status);
        Assert.DoesNotContain(PlanFeatures.TodoReminders, billing.Features, StringComparer.Ordinal);
        Assert.True(Limit(billing, PlanLimits.OpenTodos).Reached);

        // Nothing is deleted...
        var todos = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<List<Todo>>()!;
        Assert.Equal(openLimit + 1, todos.Count);

        // ...but there's no room for more, and paid features are off.
        var refused = await CreateTodoAsync(owner, "One too many", HttpStatusCode.PaymentRequired);
        Assert.Equal(PlanLimits.OpenTodos, refused.ReadAsJson<JsonObject>()!["limit"]!.GetValue<string>());
        var noReminders = await RemindAsync(owner, ids[0], HttpStatusCode.PaymentRequired);
        Assert.Equal(PlanFeatures.TodoReminders, noReminders.ReadAsJson<JsonObject>()!["feature"]!.GetValue<string>());

        // Completing Todos makes room again.
        foreach (var id in ids.Take(2))
        {
            await fixture.Host.Scenario(s =>
            {
                s.Post.Url($"/api/todos/{id}/complete");
                s.WithBearerToken(owner.AccessToken);
                s.StatusCodeShouldBe(HttpStatusCode.NoContent);
            });
        }

        await CreateTodoAsync(owner, "Fits again", HttpStatusCode.Created);
    }

    [Fact]
    public async Task reminders_use_the_months_allowance_and_stop_when_it_is_used_up()
    {
        var owner = await SignUpAsync();
        await SetAccountAsync(owner.OrganisationId, SubscriptionStatuses.Active, PlanCatalogue.Pro);
        var id = (await CreateTodoAsync(owner, "Remind me", HttpStatusCode.Created)).ReadAsJson<TodoCreationResponse>()!.Id;
        var billing = await BillingAsync(owner);
        var allowance = Limit(billing, PlanLimits.Reminders).Limit!.Value;

        // Use all but one of the month's reminders.
        using (var scope = fixture.Host.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IBillingStore>()
                .TryUseAsync(owner.OrganisationId, PlanLimits.Reminders, billing.PeriodStart, allowance - 1, allowance, CancellationToken.None));
        }

        Assert.True(Limit(await BillingAsync(owner), PlanLimits.Reminders).NearLimit);
        await RemindAsync(owner, id, HttpStatusCode.Accepted);
        await RemindAsync(owner, id, HttpStatusCode.PaymentRequired);
        Assert.Equal(allowance, Limit(await BillingAsync(owner), PlanLimits.Reminders).Used);
    }

    [Fact]
    public async Task reminders_at_the_same_moment_cannot_pass_the_allowance()
    {
        var organisationId = Guid.NewGuid();
        var periodStart = DateTimeOffset.UtcNow;
        var attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            using var scope = fixture.Host.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IBillingStore>()
                .TryUseAsync(organisationId, PlanLimits.Reminders, periodStart, 1, 5, CancellationToken.None);
        }));

        Assert.Equal(5, attempts.Count(used => used));
    }

    [Fact]
    public async Task invitations_count_towards_the_members_limit()
    {
        var owner = await SignUpAsync();
        await SetAccountAsync(owner.OrganisationId, SubscriptionStatuses.None);
        var members = PlanCatalogue.Latest(PlanCatalogue.Free).LimitOf(PlanLimits.Members)!.Value;

        // The owner is one member; invite up to the limit.
        var invited = Enumerable.Range(0, members - 1).Select(_ => $"{Guid.NewGuid():N}@example.com").ToList();
        foreach (var email in invited)
        {
            await fixture.Host.ExecuteAndWaitAsync(() => fixture.Host.Scenario(s =>
            {
                s.Post.Json(new InviteMemberRequest(email, OrganisationRoles.Member)).ToUrl("/api/organisation/invitations");
                s.WithBearerToken(owner.AccessToken);
                s.StatusCodeShouldBe(HttpStatusCode.Accepted);
            }));
        }

        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new InviteMemberRequest($"{Guid.NewGuid():N}@example.com", OrganisationRoles.Member)).ToUrl("/api/organisation/invitations");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.PaymentRequired);
        });

        // Inviting someone again replaces their invitation, so it still fits.
        await fixture.Host.ExecuteAndWaitAsync(() => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new InviteMemberRequest(invited[0], OrganisationRoles.Admin)).ToUrl("/api/organisation/invitations");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        }));
    }

    [Fact]
    public async Task a_deleted_organisation_takes_its_billing_with_it()
    {
        var owner = await SignUpAsync();
        var team = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrganisationRequest("Short-lived")).ToUrl("/api/organisations");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        })).ReadAsJson<OrganisationCreationResponse>()!.Id;
        await SetAccountAsync(team, SubscriptionStatuses.Active, PlanCatalogue.Pro);
        using (var scope = fixture.Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IBillingStore>()
                .TryUseAsync(team, PlanLimits.Reminders, DateTimeOffset.UtcNow, 1, 10, CancellationToken.None);
        }

        await fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            using var scope = fixture.Host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IOrganisationStore>().DeleteAsync(team, CancellationToken.None);
        });

        using var after = fixture.Host.Services.CreateScope();
        Assert.Null(await after.ServiceProvider.GetRequiredService<IBillingStore>().FindAccountAsync(team, CancellationToken.None));
    }

    [Fact]
    public async Task the_plans_on_offer_are_public_and_the_clients_copy_matches_them()
    {
        var result = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/billing/plans");
            s.StatusCodeShouldBeOk();
        });
        var offered = JsonNode.Parse(result.ReadAsText())!;
        Assert.Equal(PlanCatalogue.Offered.Select(p => p.Id), offered["plans"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()), StringComparer.Ordinal);

        // The pricing page is pre-rendered from src/Client/src/features/billing/plans.json. After
        // changing the plans, update it by running this test with UPDATE_PLANS=1:
        //   UPDATE_PLANS=1 dotnet test --filter the_plans_on_offer
        var copy = Path.Combine(RepositoryRoot(), "src", "Client", "src", "features", "billing", "plans.json");
        if (Environment.GetEnvironmentVariable("UPDATE_PLANS") == "1")
        {
            await File.WriteAllTextAsync(copy, offered.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        Assert.True(
            JsonNode.DeepEquals(offered, JsonNode.Parse(await File.ReadAllTextAsync(copy))),
            "The client's plans.json doesn't match GET /api/billing/plans. Update it: UPDATE_PLANS=1 dotnet test --filter the_plans_on_offer");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Client")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Can't find the repository root (with src/Client) above the test binaries");
    }

    [Fact]
    public void an_organisation_keeps_the_plan_version_it_subscribed_to()
    {
        var organisation = new Organisation { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow.AddDays(-100) };
        var account = new BillingAccount { Id = organisation.Id, PlanId = PlanCatalogue.Pro, PlanVersion = 1, Status = SubscriptionStatuses.Active };

        var state = BillingState.Resolve(organisation, account, new BillingOptions(), DateTimeOffset.UtcNow);

        Assert.Same(PlanCatalogue.Find(PlanCatalogue.Pro, 1), state.Plan);
        Assert.Equal(BillingStatuses.Active, state.Status);
    }

    [Fact]
    public void the_free_plan_follows_the_trial_and_a_cancelled_subscription()
    {
        var now = DateTimeOffset.UtcNow;
        var old = new Organisation { Id = Guid.NewGuid(), CreatedAt = now.AddDays(-15) };
        var cancelled = new BillingAccount { Id = old.Id, PlanId = PlanCatalogue.Pro, Status = SubscriptionStatuses.Canceled };

        Assert.Equal(BillingStatuses.Free, BillingState.Resolve(old, null, new BillingOptions(), now).Status);
        Assert.Equal(PlanCatalogue.Free, BillingState.Resolve(old, cancelled, new BillingOptions(), now).Plan.Id);
        Assert.Equal(BillingStatuses.Free, BillingState.Resolve(old, null, new BillingOptions { TrialDays = 0 }, now).Status);
    }

    [Theory]
    [InlineData("2026-01-31", "2026-02-15", "2026-01-31", "2026-02-28")]
    [InlineData("2026-01-31", "2026-03-31", "2026-03-31", "2026-04-30")]
    [InlineData("2026-01-10", "2026-01-09", "2026-01-10", "2026-02-10")]
    public void free_billing_periods_are_months_from_the_day_the_organisation_was_created(string created, string now, string start, string end)
    {
        static DateTimeOffset Utc(string date) => DateTimeOffset.Parse($"{date}T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var (periodStart, periodEnd) = BillingState.MonthContaining(Utc(created), Utc(now));

        Assert.Equal(Utc(start), periodStart);
        Assert.Equal(Utc(end), periodEnd);
    }
}
