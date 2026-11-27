using System.Net;
using System.Text.Json;
using Alba;
using ApiHost.Billing;
using ApiHost.Billing.Payments;
using ApiHost.Features.Admin;
using ApiHost.Features.Auth;
using ApiHost.Features.Billing;
using ApiHost.Features.Organisations;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Organisations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// --Auth saas only (excluded otherwise by template.json): the back office's dashboard,
// organisations, feature flags and failed messages.
[Collection("api")]
public class BackOfficeTests(AppFixture fixture)
{
    private const string Password = "P@ssword123!";

    private sealed record Session(string UserId, string Email, LoginResponse Tokens)
    {
        public string AccessToken => Tokens.AccessToken;

        public Guid OrganisationId => Guid.Parse(new JsonWebToken(AccessToken).GetClaim(OrganisationClaims.OrganisationId).Value);
    }

    private async Task<Session> LoginAsync(string email, string password)
    {
        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new LoginRequest(email, password)).ToUrl("/api/auth/login");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        return new Session(new JsonWebToken(tokens.AccessToken).Subject, email, tokens);
    }

    private async Task<Session> SignUpAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await fixture.RegisterConfirmedUserAsync(email, Password);
        return await LoginAsync(email, Password);
    }

    // The development user is an admin (Accounts:AdminEmails in appsettings.Development.json).
    private Task<Session> AdminAsync()
    {
        var configuration = fixture.Host.Services.GetRequiredService<IConfiguration>();
        return LoginAsync(configuration["DevelopmentUser:Email"]!, configuration["DevelopmentUser:Password"]!);
    }

    private async Task<T> GetAsync<T>(Session session, string url)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url(url);
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<T>()!;

    private Task<IScenarioResult> SendAsync(Session session, HttpMethod method, string url, object? body, HttpStatusCode expected)
        => fixture.Host.Scenario(s =>
        {
            if (method == HttpMethod.Post)
            {
                if (body is null) s.Post.Url(url); else s.Post.Json(body).ToUrl(url);
            }
            else if (method == HttpMethod.Put)
            {
                s.Put.Json(body!).ToUrl(url);
            }
            else
            {
                s.Delete.Url(url);
            }

            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBe(expected);
        });

    private async Task<Guid> CreateOrganisationAsync(Session owner, string name)
        => (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateOrganisationRequest(name)).ToUrl("/api/organisations");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        })).ReadAsJson<OrganisationCreationResponse>()!.Id;

    private async Task SetAccountAsync(Guid organisationId, string status, string planId = PlanCatalogue.Pro, string currency = "USD")
    {
        using var scope = fixture.Host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBillingStore>();
        var account = await store.FindAccountAsync(organisationId, CancellationToken.None) ?? new BillingAccount { Id = organisationId };
        account.PlanId = planId;
        account.Status = status;
        account.Currency = currency;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveAccountAsync(account, CancellationToken.None);
    }

    [Fact]
    public async Task the_back_office_is_for_admins_only()
    {
        var user = await SignUpAsync();
        foreach (var url in new[] { "/api/admin/dashboard", "/api/admin/organisations", $"/api/admin/organisations/{user.OrganisationId}", "/api/admin/features", "/api/admin/audit", "/api/admin/failed-messages" })
        {
            await fixture.Host.Scenario(s =>
            {
                s.Get.Url(url);
                s.WithBearerToken(user.AccessToken);
                s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
            });
        }
    }

    [Fact]
    public async Task the_dashboard_counts_organisations_revenue_and_failed_payments()
    {
        var admin = await AdminAsync();
        var before = await GetAsync<AdminDashboard>(admin, "/api/admin/dashboard");
        var owner = await SignUpAsync();
        var paying = await CreateOrganisationAsync(owner, "Paying");
        var failing = await CreateOrganisationAsync(owner, "Failing");
        await SetAccountAsync(paying, SubscriptionStatuses.Active, currency: "EUR");
        await SetAccountAsync(failing, SubscriptionStatuses.PastDue, currency: "EUR");

        var after = await GetAsync<AdminDashboard>(admin, "/api/admin/dashboard");

        Assert.Equal(before.NewOrganisations + 3, after.NewOrganisations);
        Assert.Equal(before.PayingOrganisations + 2, after.PayingOrganisations);
        Assert.Equal(before.FailedPayments + 1, after.FailedPayments);
        long Eur(AdminDashboard d) => d.MonthlyRevenue.FirstOrDefault(r => r.Currency == "EUR")?.MonthlyAmount ?? 0;
        Assert.Equal(Eur(before) + (2 * PlanCatalogue.Latest(PlanCatalogue.Pro).PriceIn("EUR")!.MonthlyAmount), Eur(after));
    }

    [Fact]
    public async Task an_admin_finds_an_organisation_and_sees_its_members_plan_and_usage()
    {
        var admin = await AdminAsync();
        var owner = await SignUpAsync();
        var name = $"Findable {Guid.NewGuid():N}";
        var team = await CreateOrganisationAsync(owner, name);

        var list = await GetAsync<AdminOrganisationList>(admin, $"/api/admin/organisations?search={Uri.EscapeDataString(name[^10..].ToUpperInvariant())}");
        var row = Assert.Single(list.Organisations);
        Assert.Equal((team, name, 1, BillingStatuses.Trial), (row.Id, row.Name, row.Members, row.Status));

        var detail = await GetAsync<AdminOrganisationDetail>(admin, $"/api/admin/organisations/{team}");
        Assert.Equal(owner.Email, Assert.Single(detail.Members).Email);
        Assert.Equal(PlanCatalogue.Pro, detail.Billing.PlanId);
        Assert.Contains(detail.Limits, l => l.Name == PlanLimits.Members && l.Used == 1);
        Assert.Contains(detail.Features, f => f.Name == PlanFeatures.TodoReminders && f.Enabled && f.Override is null);

        var users = await GetAsync<List<UserOrganisation>>(admin, $"/api/admin/users/{owner.UserId}/organisations");
        Assert.Contains(users, o => o.Id == team);
    }

    [Fact]
    public async Task an_admin_gives_a_plan_until_a_date_but_not_over_a_subscription()
    {
        var admin = await AdminAsync();
        var owner = await SignUpAsync();
        await SetAccountAsync(owner.OrganisationId, SubscriptionStatuses.None, PlanCatalogue.Free);
        var until = DateTimeOffset.UtcNow.AddDays(30);

        await SendAsync(admin, HttpMethod.Post, $"/api/admin/organisations/{owner.OrganisationId}/grant", new GrantPlanRequest(PlanCatalogue.Pro, until), HttpStatusCode.NoContent);

        var billing = await GetAsync<BillingSummary>(owner, "/api/billing");
        Assert.Equal((PlanCatalogue.Pro, BillingStatuses.Trial), (billing.PlanId, billing.Status));
        Assert.Equal(until, billing.TrialEndsAt!.Value, TimeSpan.FromSeconds(1));
        var audit = await GetAsync<AuditPage>(admin, $"/api/admin/audit?targetId={owner.OrganisationId}");
        Assert.Equal(AuditActions.GrantPlan, Assert.Single(audit.Entries).Action);

        await SendAsync(admin, HttpMethod.Post, $"/api/admin/organisations/{owner.OrganisationId}/grant", new GrantPlanRequest(PlanCatalogue.Pro, DateTimeOffset.UtcNow.AddDays(-1)), HttpStatusCode.BadRequest);
        await SetAccountAsync(owner.OrganisationId, SubscriptionStatuses.Active);
        await SendAsync(admin, HttpMethod.Post, $"/api/admin/organisations/{owner.OrganisationId}/grant", new GrantPlanRequest(PlanCatalogue.Pro, until), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task nobody_can_work_in_a_suspended_organisation_until_it_is_unsuspended()
    {
        var admin = await AdminAsync();
        var owner = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Suspended");
        var inTeam = owner with
        {
            Tokens = (await fixture.Host.Scenario(s =>
            {
                s.Post.Json(new SwitchOrganisationRequest(team, owner.Tokens.RefreshToken)).ToUrl("/api/organisations/switch");
                s.WithBearerToken(owner.AccessToken);
                s.StatusCodeShouldBeOk();
            })).ReadAsJson<LoginResponse>()!,
        };

        await SendAsync(admin, HttpMethod.Post, $"/api/admin/organisations/{team}/suspend", new SuspendOrganisationRequest("Unpaid invoices"), HttpStatusCode.NoContent);

        await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/todos");
            s.WithBearerToken(inTeam.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
        // A new session starts in their other organisation.
        var refreshed = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new RefreshRequest(inTeam.Tokens.RefreshToken)).ToUrl("/api/auth/refresh");
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<LoginResponse>()!;
        Assert.NotEqual(team.ToString(), new JsonWebToken(refreshed.AccessToken).GetClaim(OrganisationClaims.OrganisationId).Value, StringComparer.Ordinal);
        Assert.Equal("Unpaid invoices", (await GetAsync<AdminOrganisationDetail>(admin, $"/api/admin/organisations/{team}")).SuspensionReason);

        await SendAsync(admin, HttpMethod.Post, $"/api/admin/organisations/{team}/unsuspend", null, HttpStatusCode.NoContent);
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SwitchOrganisationRequest(team, refreshed.RefreshToken)).ToUrl("/api/organisations/switch");
            s.WithBearerToken(refreshed.AccessToken);
            s.StatusCodeShouldBeOk();
        });
    }

    [Fact]
    public async Task an_admin_keeps_notes_and_can_delete_an_organisation()
    {
        var admin = await AdminAsync();
        var owner = await SignUpAsync();
        var team = await CreateOrganisationAsync(owner, "Noted");

        await SendAsync(admin, HttpMethod.Put, $"/api/admin/organisations/{team}/notes", new OrganisationNotesRequest("Called about pricing"), HttpStatusCode.NoContent);
        Assert.Equal("Called about pricing", (await GetAsync<AdminOrganisationDetail>(admin, $"/api/admin/organisations/{team}")).Notes);

        await fixture.Host.ExecuteAndWaitAsync(() => SendAsync(admin, HttpMethod.Delete, $"/api/admin/organisations/{team}", null, HttpStatusCode.NoContent));
        await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/admin/organisations/{team}");
            s.WithBearerToken(admin.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });
    }

    [Fact]
    public async Task feature_flags_override_the_plans_most_specific_first()
    {
        var admin = await AdminAsync();
        var owner = await SignUpAsync();
        var todo = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo("Flagged")).ToUrl("/api/todos");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        })).ReadAsJson<TodoCreationResponse>()!.Id;
        Task Remind(HttpStatusCode expected) => SendAsync(owner, HttpMethod.Post, $"/api/todos/{todo}/remind", new RemindTodo(60), expected);
        Task Set(string scope, string? key, bool? enabled)
            => SendAsync(admin, HttpMethod.Put, $"/api/admin/features/{PlanFeatures.TodoReminders}", new SetFeatureRequest(scope, key, enabled), HttpStatusCode.NoContent);

        try
        {
            // Off for everyone (a kill switch), even on a plan that includes it...
            await Set(FeatureScopes.Global, null, false);
            await Remind(HttpStatusCode.PaymentRequired);

            // ...but an organisation's own override wins.
            await Set(FeatureScopes.Organisation, owner.OrganisationId.ToString(), true);
            await Remind(HttpStatusCode.Accepted);

            var features = await GetAsync<List<AdminFeature>>(admin, "/api/admin/features");
            var reminders = Assert.Single(features, f => f.Name == PlanFeatures.TodoReminders);
            Assert.False(reminders.Global);
            Assert.True(reminders.OrganisationOverrides >= 1);
        }
        finally
        {
            await Set(FeatureScopes.Global, null, null);
            await Set(FeatureScopes.Organisation, owner.OrganisationId.ToString(), null);
        }

        await Remind(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task a_failed_message_waits_for_an_admin_to_replay_it()
    {
        var admin = await AdminAsync();
        var owner = await SignUpAsync();
        var subscription = new ProviderSubscription(
            $"sub_{Guid.NewGuid():N}", $"cus_{Guid.NewGuid():N}", owner.OrganisationId, PlanCatalogue.Pro, 1, SubscriptionStatuses.Active, "USD",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), false);
        fixture.Payments.Subscriptions[subscription.Id] = subscription;
        fixture.Payments.Unreachable[subscription.Id] = true;

        // Fails in the background: the endpoint has already answered.
        await fixture.Host.Scenario(s =>
        {
            var paymentEvent = new PaymentEvent(FakePaymentProvider.ProviderName, $"evt_{Guid.NewGuid():N}", "customer.subscription.created", owner.OrganisationId, subscription.CustomerId, subscription.Id, null);
            s.Post.Text(JsonSerializer.Serialize(paymentEvent, JsonSerializerOptions.Web)).ToUrl($"/api/billing/webhooks/{FakePaymentProvider.ProviderName}");
            s.WithRequestHeader("X-Fake-Signature", FakePaymentProvider.Signature);
            s.StatusCodeShouldBeOk();
        });

        var failed = await Eventually(async () =>
            (await GetAsync<FailedMessagePage>(admin, "/api/admin/failed-messages")).Messages
                .FirstOrDefault(m => m.ExceptionMessage?.Contains(subscription.Id, StringComparison.Ordinal) == true));
        Assert.Contains(nameof(PaymentEventReceived), failed.MessageType, StringComparison.Ordinal);
        Assert.Equal(BillingStatuses.Trial, (await GetAsync<BillingSummary>(owner, "/api/billing")).Status);

        // The provider is back: replay it.
        fixture.Payments.Unreachable.TryRemove(subscription.Id, out _);
        await SendAsync(admin, HttpMethod.Post, "/api/admin/failed-messages/replay", new FailedMessagesRequest([failed.Id]), HttpStatusCode.NoContent);

        await Eventually(async () => (await GetAsync<BillingSummary>(owner, "/api/billing")).Status == BillingStatuses.Active ? "active" : null);
    }

    // Waits up to 30 seconds for something that happens in the background.
    private static async Task<T> Eventually<T>(Func<Task<T?>> check)
        where T : class
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await check() is { } found)
            {
                return found;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Gave up waiting");
    }
}
