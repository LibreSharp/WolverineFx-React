using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Wolverine;

namespace ApiHost.Integrations.Webhooks;

// Thrown so Wolverine retries a delivery later (see IntegrationsSetup's error policy); after the
// last retry it's parked with the failed messages, and owners can still resend it.
public class WebhookDeliveryFailedException(string message) : Exception(message);

public static class WebhookHandler
{
    // An event: one delivery for each of the organisation's subscriptions that wants it, each sent
    // on its own (so one slow or failing receiver doesn't hold up the others).
    public static async Task<OutgoingMessages> Handle(WebhookEventRaised raised, IWebhookStore store, IOptions<WebhookOptions> options, TimeProvider clock, CancellationToken cancellationToken)
    {
        var subscriptions = (await store.SubscriptionsAsync(raised.OrganisationId, cancellationToken)).Where(s => s.Wants(raised.Type)).ToList();
        return await DeliverAsync(raised, subscriptions, store, options.Value, clock, cancellationToken);
    }

    public static async Task<OutgoingMessages> DeliverAsync(
        WebhookEventRaised raised, IReadOnlyList<WebhookSubscription> subscriptions, IWebhookStore store, WebhookOptions options, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (subscriptions.Count == 0)
        {
            return [];
        }

        var now = clock.GetUtcNow();
        var payload = PayloadOf(raised);
        var deliveries = subscriptions.Select(subscription => new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            OrganisationId = raised.OrganisationId,
            SubscriptionId = subscription.Id,
            EventId = raised.EventId,
            EventType = raised.Type,
            Payload = payload,
            CreatedAt = now,
        }).ToList();
        await store.AddDeliveriesAsync(deliveries, raised.OrganisationId, now - options.KeepDeliveriesFor, cancellationToken);

        var messages = new OutgoingMessages();
        messages.AddRange(deliveries.Select(d => new DeliverWebhook(d.Id)));
        return messages;
    }

    public static async Task Handle(DeliverWebhook message, IWebhookStore store, WebhookSender sender, CancellationToken cancellationToken)
    {
        var delivery = await store.FindDeliveryAsync(message.DeliveryId, cancellationToken);
        if (delivery is null || (delivery.DeliveredAt is not null && !message.Again))
        {
            return;
        }

        // Deleted or turned off since: nothing to send.
        var subscription = await store.FindSubscriptionAsync(delivery.OrganisationId, delivery.SubscriptionId, cancellationToken);
        if (subscription is not { Enabled: true })
        {
            return;
        }

        var delivered = await sender.SendAsync(subscription, delivery, cancellationToken);
        await store.RecordAttemptAsync(delivery, cancellationToken);
        if (!delivered)
        {
            throw new WebhookDeliveryFailedException($"Webhook {delivery.Id} to {subscription.Url} failed ({delivery.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "no answer"})");
        }
    }

    // { "id", "type", "timestamp", "data" }: the body every delivery of the event gets.
    private static string PayloadOf(WebhookEventRaised raised)
        => new JsonObject
        {
            ["id"] = raised.EventId,
            ["type"] = raised.Type,
            ["timestamp"] = raised.OccurredAt,
            ["data"] = JsonNode.Parse(raised.DataJson),
        }.ToJsonString(JsonSerializerOptions.Web);
}
