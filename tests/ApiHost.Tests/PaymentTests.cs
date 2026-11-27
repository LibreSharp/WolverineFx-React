using System.Net;
using System.Text.Json;
using Alba;
using ApiHost.Billing;
using ApiHost.Billing.Payments;
using ApiHost.Billing.Stripe;
using ApiHost.Features.Auth;
using ApiHost.Features.Billing;
using ApiHost.Identity;
using ApiHost.Organisations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using EventUtility = Stripe.EventUtility;
using StripeConfiguration = Stripe.StripeConfiguration;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// --Auth saas only (excluded otherwise by template.json): checkout, the customer portal, and
// webhooks keeping the local copy of billing state in step, with a fake payment provider
// (FakePaymentProvider). The Stripe provider's own webhook checks are at the end.
[Collection("api")]
public class PaymentTests(AppFixture fixture)
{
    private const string Password = "P@ssword123!";

    private sealed record Session(string UserId, string Email, string AccessToken)
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
        return new Session(new JsonWebToken(tokens.AccessToken).Subject, email, tokens.AccessToken);
    }

    private Task<IScenarioResult> CheckoutAsync(Session session, string planId, HttpStatusCode expected, string? currency = null)
        => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CheckoutRequestBody(planId, currency)).ToUrl("/api/billing/checkout");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBe(expected);
        });

    private async Task<BillingSummary> BillingAsync(Session session)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/billing");
            s.WithBearerToken(session.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<BillingSummary>()!;

    // Delivers a webhook, as the provider would, and waits for its handling.
    private Task<ITrackedSession> DeliverAsync(PaymentEvent paymentEvent, string signature = FakePaymentProvider.Signature, HttpStatusCode expected = HttpStatusCode.OK)
        => fixture.Host.ExecuteAndWaitAsync(() => fixture.Host.Scenario(s =>
        {
            s.Post.Text(JsonSerializer.Serialize(paymentEvent, JsonSerializerOptions.Web)).ToUrl($"/api/billing/webhooks/{FakePaymentProvider.ProviderName}");
            s.WithRequestHeader("X-Fake-Signature", signature);
            s.StatusCodeShouldBe(expected);
        }));

    private ProviderSubscription Subscribe(Guid organisationId, string status = SubscriptionStatuses.Active, string? id = null, string customerId = "cus_1")
    {
        var subscription = new ProviderSubscription(
            id ?? $"sub_{Guid.NewGuid():N}", customerId, organisationId, PlanCatalogue.Pro, 1, status, "USD",
            DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(27), false);
        fixture.Payments.Subscriptions[subscription.Id] = subscription;
        return subscription;
    }

    private static PaymentEvent SubscriptionEvent(ProviderSubscription subscription, string type = "customer.subscription.updated")
        => new(FakePaymentProvider.ProviderName, $"evt_{Guid.NewGuid():N}", type, subscription.OrganisationId, subscription.CustomerId, subscription.Id, null);

    [Fact]
    public async Task an_owner_checks_out_on_the_providers_page_keeping_the_trial()
    {
        var owner = await SignUpAsync();

        var page = (await CheckoutAsync(owner, PlanCatalogue.Pro, HttpStatusCode.OK, "eur")).ReadAsJson<PaymentPageResponse>()!;

        var checkout = Assert.Single(fixture.Payments.Checkouts, c => c.OrganisationId == owner.OrganisationId);
        Assert.Equal($"https://payments.example.com/checkout/{owner.OrganisationId}", page.Url);
        Assert.Equal(PlanCatalogue.Latest(PlanCatalogue.Pro), checkout.Plan);
        Assert.Equal("EUR", checkout.Currency);
        Assert.Equal(owner.Email, checkout.Email);
        Assert.NotNull(checkout.TrialEndsAt);
        Assert.Equal("/organisation", checkout.SuccessUrl.AbsolutePath);
    }

    [Fact]
    public async Task checkout_is_for_owners_and_paid_plans_in_their_currencies()
    {
        var owner = await SignUpAsync();
        var member = await SignUpAsync();
        using (var scope = fixture.Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOrganisationStore>().AddMembershipAsync(new Membership
            {
                Id = Guid.CreateVersion7(),
                OrganisationId = owner.OrganisationId,
                UserId = member.UserId,
                Role = OrganisationRoles.Admin,
                JoinedAt = DateTimeOffset.UtcNow,
            }, CancellationToken.None);
        }

        var memberThere = member with
        {
            AccessToken = (await fixture.Host.Scenario(s =>
            {
                s.Post.Json(new LoginRequest(member.Email, Password)).ToUrl("/api/auth/login");
                s.StatusCodeShouldBeOk();
            })).ReadAsJson<LoginResponse>()!.AccessToken,
        };
        if (memberThere.OrganisationId == owner.OrganisationId)
        {
            await CheckoutAsync(memberThere, PlanCatalogue.Pro, HttpStatusCode.Forbidden);
        }

        await CheckoutAsync(owner, PlanCatalogue.Free, HttpStatusCode.BadRequest);
        await CheckoutAsync(owner, "no-such-plan", HttpStatusCode.BadRequest);
        await CheckoutAsync(owner, PlanCatalogue.Pro, HttpStatusCode.BadRequest, "JPY");
    }

    [Fact]
    public async Task a_webhook_puts_the_organisation_on_its_plan_and_opens_the_portal()
    {
        var owner = await SignUpAsync();
        var subscription = Subscribe(owner.OrganisationId, customerId: $"cus_{Guid.NewGuid():N}");

        await DeliverAsync(SubscriptionEvent(subscription, "checkout.session.completed"));

        var billing = await BillingAsync(owner);
        Assert.Equal(BillingStatuses.Active, billing.Status);
        Assert.Equal(PlanCatalogue.Pro, billing.PlanId);
        Assert.Null(billing.TrialEndsAt);
        Assert.True(billing.HasCustomer);
        Assert.Equal(subscription.PeriodStart, billing.PeriodStart, TimeSpan.FromMilliseconds(1));

        var portal = (await fixture.Host.Scenario(s =>
        {
            s.Post.Url("/api/billing/portal");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<PaymentPageResponse>()!;
        Assert.Equal($"https://payments.example.com/portal/{subscription.CustomerId}", portal.Url);

        // Already subscribed: plan changes go through the portal.
        await CheckoutAsync(owner, PlanCatalogue.Pro, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task a_webhook_without_a_valid_signature_is_refused_and_a_repeated_one_is_ignored()
    {
        var owner = await SignUpAsync();
        var subscription = Subscribe(owner.OrganisationId);
        var paymentEvent = SubscriptionEvent(subscription);

        await DeliverAsync(paymentEvent, signature: "forged", expected: HttpStatusCode.BadRequest);
        Assert.Equal(BillingStatuses.Trial, (await BillingAsync(owner)).Status);

        var first = await DeliverAsync(paymentEvent);
        var again = await DeliverAsync(paymentEvent);

        Assert.Single(first.Executed.MessagesOf<PaymentEventReceived>());
        Assert.Empty(again.Executed.MessagesOf<PaymentEventReceived>());
        Assert.Equal(BillingStatuses.Active, (await BillingAsync(owner)).Status);
    }

    [Fact]
    public async Task a_late_cancellation_of_an_old_subscription_does_not_end_the_new_one()
    {
        var owner = await SignUpAsync();
        var old = Subscribe(owner.OrganisationId, SubscriptionStatuses.Canceled);
        var current = Subscribe(owner.OrganisationId);

        await DeliverAsync(SubscriptionEvent(current));
        await DeliverAsync(SubscriptionEvent(old, "customer.subscription.deleted"));

        Assert.Equal(BillingStatuses.Active, (await BillingAsync(owner)).Status);

        // The current one ending puts the organisation on the free plan; nothing is deleted.
        fixture.Payments.Subscriptions[current.Id] = current with { Status = SubscriptionStatuses.Canceled };
        await DeliverAsync(SubscriptionEvent(current, "customer.subscription.deleted"));
        Assert.Equal(BillingStatuses.Free, (await BillingAsync(owner)).Status);
    }

    [Fact]
    public async Task a_failed_payment_keeps_the_plan_and_invoices_are_kept()
    {
        var owner = await SignUpAsync();
        var subscription = Subscribe(owner.OrganisationId, customerId: $"cus_{Guid.NewGuid():N}");
        await DeliverAsync(SubscriptionEvent(subscription));
        fixture.Payments.Subscriptions[subscription.Id] = subscription with { Status = SubscriptionStatuses.PastDue };

        var invoice = new ProviderInvoice(
            $"in_{Guid.NewGuid():N}", subscription.CustomerId, subscription.Id, "INV-1", "open", 1200, 0, "USD",
            DateTimeOffset.UtcNow, subscription.PeriodStart, subscription.PeriodEnd, "https://payments.example.com/i", null);
        // Found by its customer: invoice events don't name the organisation.
        await DeliverAsync(new PaymentEvent(FakePaymentProvider.ProviderName, $"evt_{Guid.NewGuid():N}", "invoice.payment_failed", null, subscription.CustomerId, subscription.Id, invoice));

        var billing = await BillingAsync(owner);
        Assert.Equal(BillingStatuses.PastDue, billing.Status);
        Assert.Contains(PlanFeatures.TodoReminders, billing.Features, StringComparer.Ordinal);

        using var scope = fixture.Host.Services.CreateScope();
        var saved = Assert.Single(await scope.ServiceProvider.GetRequiredService<IBillingStore>().InvoicesAsync(owner.OrganisationId, CancellationToken.None));
        Assert.Equal(("INV-1", "open", 1200L), (saved.Number, saved.Status, saved.Total));
    }

    [Fact]
    public async Task deleting_an_organisation_cancels_its_subscription()
    {
        var owner = await SignUpAsync();
        var team = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new ApiHost.Features.Organisations.CreateOrganisationRequest("Paying")).ToUrl("/api/organisations");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        })).ReadAsJson<ApiHost.Features.Organisations.OrganisationCreationResponse>()!.Id;
        var subscription = Subscribe(team);
        await DeliverAsync(SubscriptionEvent(subscription));

        await fixture.Host.ExecuteAndWaitAsync(async () =>
        {
            using var scope = fixture.Host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IOrganisationStore>().DeleteAsync(team, CancellationToken.None);
        });

        Assert.Contains(subscription.Id, fixture.Payments.Cancelled, StringComparer.Ordinal);
    }

    // The Stripe provider, without contacting Stripe: webhooks are signed with the webhook secret.
    private const string WebhookSecret = "whsec_test_secret";

    private static StripePaymentProvider Stripe()
        => new(Options.Create(new StripeOptions { SecretKey = "sk_test_x", WebhookSecret = WebhookSecret }), TimeProvider.System);

    private static string CheckoutCompleted(Guid organisationId) => JsonSerializer.Serialize(new
    {
        id = "evt_1",
        @object = "event",
        type = "checkout.session.completed",
        api_version = StripeConfiguration.ApiVersion,
        created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        data = new
        {
            @object = new
            {
                id = "cs_1",
                @object = "checkout.session",
                mode = "subscription",
                client_reference_id = organisationId.ToString(),
                customer = "cus_1",
                subscription = "sub_1",
            },
        },
    });

    private static HeaderDictionary SignedWith(string payload, string secret)
        => new() { ["Stripe-Signature"] = EventUtility.GenerateSignatureHeader(payload, secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds()) };

    [Fact]
    public void stripe_webhooks_are_read_only_with_the_right_signature()
    {
        var organisationId = Guid.NewGuid();
        var payload = CheckoutCompleted(organisationId);

        var read = Stripe().ReadWebhook(payload, SignedWith(payload, WebhookSecret));

        Assert.Equal(new PaymentEvent(StripePaymentProvider.ProviderName, "evt_1", "checkout.session.completed", organisationId, "cus_1", "sub_1", null), read);
        Assert.Null(Stripe().ReadWebhook(payload, SignedWith(payload, "whsec_someone_else")));
        Assert.Null(Stripe().ReadWebhook(payload.Replace("cus_1", "cus_2", StringComparison.Ordinal), SignedWith(payload, WebhookSecret)));
        Assert.Null(Stripe().ReadWebhook(payload, new HeaderDictionary()));
    }

    [Theory]
    [InlineData("active", SubscriptionStatuses.Active)]
    [InlineData("trialing", SubscriptionStatuses.Active)]
    [InlineData("past_due", SubscriptionStatuses.PastDue)]
    [InlineData("unpaid", SubscriptionStatuses.PastDue)]
    [InlineData("canceled", SubscriptionStatuses.Canceled)]
    [InlineData("incomplete", SubscriptionStatuses.None)]
    public void stripe_statuses_map_to_the_apps(string stripeStatus, string expected)
        => Assert.Equal(expected, StripePaymentProvider.StatusOf(stripeStatus));

    [Fact]
    public void stripe_prices_are_found_by_their_plans_lookup_keys()
    {
        var pro = PlanCatalogue.Find(PlanCatalogue.Pro, 1)!;
        Assert.Equal("pro-v1-eur", StripePaymentProvider.LookupKey(pro, "EUR"));
        Assert.Equal((PlanCatalogue.Pro, 1), StripePaymentProvider.PlanOf("pro-v1-eur"));
        Assert.Null(StripePaymentProvider.PlanOf("price_from_the_dashboard"));
    }
}
