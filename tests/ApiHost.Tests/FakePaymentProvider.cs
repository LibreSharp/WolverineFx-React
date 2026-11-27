using System.Collections.Concurrent;
using System.Text.Json;
using ApiHost.Billing;
using ApiHost.Billing.Payments;
using Microsoft.AspNetCore.Http;

namespace ApiHost.Tests;

// Stands in for Stripe in the API tests (AppFixture): records checkouts and cancellations, keeps
// subscriptions that a test puts in it, and accepts a webhook whose X-Fake-Signature header is
// Signature, with a PaymentEvent as its body. Nothing contacts Stripe.
public class FakePaymentProvider : IPaymentProvider
{
    public const string ProviderName = "fake";
    public const string Signature = "valid";

    public ConcurrentDictionary<string, ProviderSubscription> Subscriptions { get; } = new(StringComparer.Ordinal);

    public ConcurrentQueue<CheckoutRequest> Checkouts { get; } = new();

    public ConcurrentQueue<string> Cancelled { get; } = new();

    public string Name => ProviderName;

    public bool IsConfigured => true;

    public Task<Uri> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        Checkouts.Enqueue(request);
        return Task.FromResult(new Uri($"https://payments.example.com/checkout/{request.OrganisationId}"));
    }

    public Task<Uri> CreatePortalAsync(string customerId, Uri returnUrl, CancellationToken cancellationToken)
        => Task.FromResult(new Uri($"https://payments.example.com/portal/{customerId}"));

    public Task<ProviderSubscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
        => Task.FromResult(Subscriptions.GetValueOrDefault(subscriptionId));

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        Cancelled.Enqueue(subscriptionId);
        if (Subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            Subscriptions[subscriptionId] = subscription with { Status = SubscriptionStatuses.Canceled };
        }

        return Task.CompletedTask;
    }

    public PaymentEvent? ReadWebhook(string payload, IHeaderDictionary headers)
        => headers["X-Fake-Signature"] == Signature ? JsonSerializer.Deserialize<PaymentEvent>(payload, JsonSerializerOptions.Web) : null;

    public Uri? DashboardLink(string kind, string id) => new($"https://payments.example.com/{kind}/{id}");
}
