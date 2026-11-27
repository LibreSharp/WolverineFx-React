using System.Net;
using System.Text.Json;
using Alba;
using ApiHost.Features.Auth;
using ApiHost.Features.Integrations;
using ApiHost.Features.Organisations;
using ApiHost.Features.Todos;
using ApiHost.Identity;
using ApiHost.Integrations.Secrets;
using ApiHost.Integrations.Webhooks;
using ApiHost.Organisations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Wolverine.Tracking;

namespace ApiHost.Tests;

// --Auth saas only (excluded otherwise by template.json): outgoing webhooks and encrypted secrets.
[Collection("api")]
public class WebhookTests(AppFixture fixture)
{
    private const string Password = "P@ssword123!";

    private sealed record Session(string UserId, LoginResponse Tokens)
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
        return new Session(new JsonWebToken(tokens.AccessToken).Subject, tokens);
    }

    private static string UniqueUrl() => $"https://receiver.example.com/{Guid.NewGuid():N}";

    private async Task<CreatedWebhook> SubscribeAsync(Session owner, string url, params string[] events)
        => (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SaveWebhookRequest(url, "Tests", events)).ToUrl("/api/organisation/webhooks");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<CreatedWebhook>()!;

    private Task<IScenarioResult> CreateTodoAsync(Session owner, string title)
        => fixture.Host.Scenario(s =>
        {
            s.Post.Json(new CreateTodo(title)).ToUrl("/api/todos");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Created);
        });

    private async Task<List<DeliverySummary>> DeliveriesAsync(Session owner, Guid webhookId)
        => (await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/organisation/webhooks/{webhookId}/deliveries");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBeOk();
        })).ReadAsJson<List<DeliverySummary>>()!;

    private static async Task<T> Eventually<T>(Func<T?> check)
        where T : class
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (check() is { } found)
            {
                return found;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Gave up waiting");
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> check)
        where T : class
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await check() is { } found)
            {
                return found;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Gave up waiting");
    }

    [Fact]
    public async Task subscribers_get_signed_events_they_asked_for()
    {
        var owner = await SignUpAsync();
        var everything = UniqueUrl();
        var completedOnly = UniqueUrl();
        var all = await SubscribeAsync(owner, everything);
        await SubscribeAsync(owner, completedOnly, WebhookEvents.TodoCompleted);

        var id = (await CreateTodoAsync(owner, "Ship it")).ReadAsJson<TodoCreationResponse>()!.Id;
        var received = await Eventually(() => fixture.Webhooks.To(everything).FirstOrDefault());

        // Standard Webhooks: the signature checks out with the secret the owner was given, and the
        // body says what happened.
        Assert.True(WebhookSignatures.Check(all.Secret, received.Headers["webhook-id"], received.Headers["webhook-timestamp"], received.Headers["webhook-signature"], received.Body, DateTimeOffset.UtcNow));
        Assert.False(WebhookSignatures.Check(WebhookSignatures.GenerateSecret(), received.Headers["webhook-id"], received.Headers["webhook-timestamp"], received.Headers["webhook-signature"], received.Body, DateTimeOffset.UtcNow));
        using var body = JsonDocument.Parse(received.Body);
        Assert.Equal(WebhookEvents.TodoCreated, body.RootElement.GetProperty("type").GetString());
        Assert.Equal(id, body.RootElement.GetProperty("data").GetProperty("id").GetGuid());
        Assert.Equal("Ship it", body.RootElement.GetProperty("data").GetProperty("title").GetString());

        // Completing it reaches both; creating it reached only the one that wanted everything.
        await fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/todos/{id}/complete");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var completed = await Eventually(() => fixture.Webhooks.To(completedOnly).FirstOrDefault());
        Assert.Contains(WebhookEvents.TodoCompleted, completed.Body, StringComparison.Ordinal);
        await Eventually(() => fixture.Webhooks.To(everything).Count == 2 ? "both" : null);
        Assert.Single(fixture.Webhooks.To(completedOnly));

        // The secret can be shown again to owners and admins.
        var shown = (await fixture.Host.Scenario(s =>
        {
            s.Get.Url($"/api/organisation/webhooks/{all.Webhook.Id}/secret");
            s.WithBearerToken(owner.AccessToken);
        })).ReadAsJson<WebhookSecret>()!;
        Assert.Equal(all.Secret, shown.Secret);
    }

    [Fact]
    public async Task failed_deliveries_are_recorded_and_can_be_resent()
    {
        var owner = await SignUpAsync();
        var url = UniqueUrl();
        fixture.Webhooks.Answer(url, HttpStatusCode.InternalServerError);
        var webhook = await SubscribeAsync(owner, url);

        await CreateTodoAsync(owner, "Will fail at first");
        var failed = await EventuallyAsync(async () => (await DeliveriesAsync(owner, webhook.Webhook.Id)).FirstOrDefault(d => d.Attempts > 0));
        Assert.Equal(500, failed.StatusCode);
        Assert.Null(failed.DeliveredAt);
        Assert.Equal("receiver is down", failed.Response);

        // The receiver is back: resending delivers it with the same event id.
        fixture.Webhooks.Answer(url, HttpStatusCode.OK);
        await fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/organisation/webhooks/deliveries/{failed.Id}/resend");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        });
        var delivered = await EventuallyAsync(async () => (await DeliveriesAsync(owner, webhook.Webhook.Id)).FirstOrDefault(d => d.DeliveredAt is not null));
        Assert.Equal(200, delivered.StatusCode);
        Assert.All(fixture.Webhooks.To(url), r => Assert.Equal(failed.EventId.ToString(), r.Headers["webhook-id"]));
    }

    [Fact]
    public async Task a_test_event_goes_to_that_webhook_only()
    {
        var owner = await SignUpAsync();
        var tested = UniqueUrl();
        var other = UniqueUrl();
        var webhook = await SubscribeAsync(owner, tested);
        await SubscribeAsync(owner, other);

        await fixture.Host.Scenario(s =>
        {
            s.Post.Url($"/api/organisation/webhooks/{webhook.Webhook.Id}/test");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.Accepted);
        });

        var received = await Eventually(() => fixture.Webhooks.To(tested).FirstOrDefault());
        Assert.Contains(WebhookEvents.Test, received.Body, StringComparison.Ordinal);
        Assert.Empty(fixture.Webhooks.To(other));
    }

    [Fact]
    public async Task webhook_urls_and_events_are_checked_and_members_cannot_manage_them()
    {
        var owner = await SignUpAsync();
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SaveWebhookRequest("not a url", null, null)).ToUrl("/api/organisation/webhooks");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SaveWebhookRequest(UniqueUrl(), null, ["todo.exploded"])).ToUrl("/api/organisation/webhooks");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        var member = await SignUpAsync();
        using (var scope = fixture.Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOrganisationStore>().AddMembershipAsync(new Membership
            {
                Id = Guid.CreateVersion7(),
                OrganisationId = owner.OrganisationId,
                UserId = member.UserId,
                Role = OrganisationRoles.Member,
                JoinedAt = DateTimeOffset.UtcNow,
            }, CancellationToken.None);
        }

        var tokens = (await fixture.Host.Scenario(s =>
        {
            s.Post.Json(new SwitchOrganisationRequest(owner.OrganisationId, member.Tokens.RefreshToken)).ToUrl("/api/organisations/switch");
            s.WithBearerToken(member.AccessToken);
        })).ReadAsJson<LoginResponse>()!;
        foreach (var url in new[] { "/api/organisation/webhooks", "/api/organisation/secrets" })
        {
            await fixture.Host.Scenario(s =>
            {
                s.Get.Url(url);
                s.WithBearerToken(tokens.AccessToken);
                s.StatusCodeShouldBe(HttpStatusCode.Forbidden);
            });
        }
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.5", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("93.184.216.34", true)]
    [InlineData("2606:4700::1111", true)]
    public void webhooks_never_go_to_private_networks_in_production(string address, bool allowed)
        => Assert.Equal(allowed, PrivateNetworks.IsPublic(IPAddress.Parse(address)));

    [Fact]
    public void old_or_tampered_webhooks_fail_the_signature_check()
    {
        var secret = WebhookSignatures.GenerateSecret();
        var sentAt = DateTimeOffset.UtcNow;
        var signature = WebhookSignatures.Sign(secret, "evt_1", sentAt, "{\"a\":1}");
        var timestamp = sentAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(WebhookSignatures.Check(secret, "evt_1", timestamp, signature, "{\"a\":1}", sentAt));
        Assert.False(WebhookSignatures.Check(secret, "evt_1", timestamp, signature, "{\"a\":2}", sentAt));
        Assert.False(WebhookSignatures.Check(secret, "evt_2", timestamp, signature, "{\"a\":1}", sentAt));
        Assert.False(WebhookSignatures.Check(secret, "evt_1", timestamp, signature, "{\"a\":1}", sentAt.AddMinutes(10)));
    }

    private Task<IScenarioResult> ReceiveAsync(string source, string id, string body, string secret, HttpStatusCode expected)
    {
        var sentAt = DateTimeOffset.UtcNow;
        return fixture.Host.Scenario(s =>
        {
            s.Post.Text(body).ToUrl($"/api/webhooks/incoming/{source}");
            s.WithRequestHeader(WebhookSignatures.IdHeader, id);
            s.WithRequestHeader(WebhookSignatures.TimestampHeader, sentAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            s.WithRequestHeader(WebhookSignatures.SignatureHeader, WebhookSignatures.Sign(secret, id, sentAt, body));
            s.StatusCodeShouldBe(expected);
        });
    }

    [Fact]
    public async Task incoming_webhooks_are_checked_and_handled_once()
    {
        var id = $"msg_{Guid.NewGuid():N}";
        const string body = "{\"type\":\"message.created\"}";

        var tracked = await fixture.Host.ExecuteAndWaitAsync(() => ReceiveAsync("example", id, body, AppFixture.IncomingWebhookSecret, HttpStatusCode.NoContent));
        var received = Assert.Single(tracked.Executed.MessagesOf<IncomingWebhookReceived>());
        Assert.Equal(("example", id, body), (received.Source, received.EventId, received.Body));

        // The provider sending it again is answered, and ignored.
        var again = await fixture.Host.ExecuteAndWaitAsync(() => ReceiveAsync("example", id, body, AppFixture.IncomingWebhookSecret, HttpStatusCode.NoContent));
        Assert.Empty(again.Executed.MessagesOf<IncomingWebhookReceived>());

        await ReceiveAsync("example", $"msg_{Guid.NewGuid():N}", body, WebhookSignatures.GenerateSecret(), HttpStatusCode.Unauthorized);
        await ReceiveAsync("unknown", $"msg_{Guid.NewGuid():N}", body, AppFixture.IncomingWebhookSecret, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task secrets_are_stored_encrypted_and_never_shown_again()
    {
        var owner = await SignUpAsync();
        const string value = "sk-live-0123456789abcdef";
        await fixture.Host.Scenario(s =>
        {
            s.Put.Json(new SetSecretRequest(value)).ToUrl("/api/organisation/secrets/openai-api-key");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await fixture.Host.Scenario(s =>
        {
            s.Put.Json(new SetSecretRequest(value)).ToUrl("/api/organisation/secrets/Not%20Allowed");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });

        var listed = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/organisation/secrets");
            s.WithBearerToken(owner.AccessToken);
        });
        Assert.DoesNotContain(value, listed.ReadAsText(), StringComparison.Ordinal);
        var summary = Assert.Single(listed.ReadAsJson<List<SecretSummary>>()!);
        Assert.Equal(("openai-api-key", "cdef"), (summary.Name, summary.Hint));

        // Encrypted in the database; the app's own code reads it back.
        using var scope = fixture.Host.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ISecretStore>().FindAsync(owner.OrganisationId, "openai-api-key", CancellationToken.None);
        Assert.DoesNotContain(value, stored!.ProtectedValue, StringComparison.Ordinal);
        var secrets = scope.ServiceProvider.GetRequiredService<OrganisationSecrets>();
        Assert.Equal(value, await secrets.GetAsync(owner.OrganisationId, "openai-api-key", CancellationToken.None));

        // Another organisation's code can't read it.
        Assert.Null(await secrets.GetAsync(Guid.CreateVersion7(), "openai-api-key", CancellationToken.None));

        await fixture.Host.Scenario(s =>
        {
            s.Delete.Url("/api/organisation/secrets/openai-api-key");
            s.WithBearerToken(owner.AccessToken);
            s.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        Assert.Null(await secrets.GetAsync(owner.OrganisationId, "openai-api-key", CancellationToken.None));
    }
}
