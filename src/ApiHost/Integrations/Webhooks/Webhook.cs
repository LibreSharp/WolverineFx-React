using System.Text.Json;

namespace ApiHost.Integrations.Webhooks;

// A URL an organisation wants told about events, such as a Todo being completed. Each delivery
// is signed with the subscription's secret (Standard Webhooks; see WebhookSignatures), so the
// receiver can check it came from this app. Not tenanted: it names its organisation.
public class WebhookSubscription
{
    public Guid Id { get; set; }

    public Guid OrganisationId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // The event types it wants (WebhookEvents); empty for all of them.
    public IList<string> Events { get; set; } = [];

    // The signing secret, encrypted with ASP.NET Core Data Protection (SecretProtection).
    public string ProtectedSecret { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public bool Wants(string eventType) => Enabled && (Events.Count == 0 || Events.Contains(eventType, StringComparer.Ordinal));
}

// One event sent, or being sent, to one subscription: what was sent and how the receiver answered
// the last attempt. Owners see these to find failures and resend.
public class WebhookDelivery
{
    public Guid Id { get; set; }

    public Guid OrganisationId { get; set; }

    public Guid SubscriptionId { get; set; }

    // The event's id: the same for every attempt and every subscription, so receivers can ignore
    // repeats (the webhook-id header).
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    // The JSON body sent.
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    // The receiver's status code, or null if it couldn't be reached.
    public int? StatusCode { get; set; }

    // The start of the receiver's answer, or why it couldn't be reached.
    public string? Response { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
}

// The events an organisation can subscribe to. Add a constant (with its description) when a
// feature raises a new one with WebhookEvents.Raise.
public static class WebhookEvents
{
    public const string TodoCreated = "todo.created";
    public const string TodoCompleted = "todo.completed";

    // Sent by "Send test event", and to no one else.
    public const string Test = "webhook.test";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TodoCreated] = "A Todo was added.",
        [TodoCompleted] = "A Todo was completed.",
    };

    // The message a feature publishes (from an endpoint, a handler or an MCP tool, through the
    // outbox) when something happens that subscribers may want to hear about.
    public static WebhookEventRaised Raise<T>(Guid organisationId, string type, T data)
        => new(organisationId, Guid.CreateVersion7(), type, DateTimeOffset.UtcNow, JsonSerializer.Serialize(data, JsonSerializerOptions.Web));
}

// Handled by WebhookHandler: one WebhookDelivery for each subscription that wants it.
public record WebhookEventRaised(Guid OrganisationId, Guid EventId, string Type, DateTimeOffset OccurredAt, string DataJson);

// Sends one delivery, from a durable queue: retried with backoff when the receiver fails. Again:
// send it even if it was delivered before (an owner's "Resend").
public record DeliverWebhook(Guid DeliveryId, bool Again = false);

// What a Todo event's "data" is.
public record TodoWebhookData(Guid Id, string Title, bool IsComplete);

public class WebhookOptions
{
    public const string SectionName = "Webhooks";

    public int MaxPerOrganisation { get; set; } = 10;

    // How long a receiver has to answer.
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    // Deliveries older than this are deleted when new ones are made.
    public TimeSpan KeepDeliveriesFor { get; set; } = TimeSpan.FromDays(30);

    // Lets webhooks go to localhost and private networks, and over plain HTTP. On in Development
    // only: in production a webhook URL could otherwise be used to reach the app's own network.
    public bool AllowPrivateNetworks { get; set; }
}
