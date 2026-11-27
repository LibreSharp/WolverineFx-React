using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Wolverine.Http;

namespace ApiHost.Integrations.Webhooks;

// Receiving webhooks from other services, the same way billing receives Stripe's: check the
// signature, store the event once, hand it to a durable message, and answer at once. The work
// happens in a handler, which is retried if it fails, so a slow or failing handler never makes
// the sender retry or give up.
//
// Any provider that signs the Standard Webhooks way (many do; see WebhookSignatures) works with
// no code: add its signing secret under Webhooks:Incoming:Secrets:{source}, point the provider at
// /api/webhooks/incoming/{source}, and handle IncomingWebhookReceived (below) for that source.
// For a provider that signs differently (Stripe, GitHub, Slack), copy the endpoint and change the
// check: Billing/Stripe and Features/Billing/PaymentEndpoints.cs are a worked example.
public class IncomingWebhookOptions
{
    public const string SectionName = "Webhooks:Incoming";

    // Signing secret ("whsec_...") by source name, such as "chat".
    public IDictionary<string, string> Secrets { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

// A received event, once per event id. Body is the JSON the provider sent.
public record IncomingWebhookReceived(string Source, string EventId, string Body);

// Stored for each received event, so a second delivery of it is ignored. Id: "{source}:{event id}".
public class IncomingWebhookRecord
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }
}

public static class IncomingWebhookEndpoint
{
    // The most a received body may be.
    private const int MaxBodyLength = 256 * 1024;

    [AllowAnonymous]
    [WolverinePost("/api/webhooks/incoming/{source}")]
    public static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest>> Post(
        string source, HttpRequest request, IOptions<IncomingWebhookOptions> options, IWebhookStore store, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!options.Value.Secrets.TryGetValue(source, out var secret) || string.IsNullOrEmpty(secret))
        {
            return TypedResults.NotFound();
        }

        if (request.ContentLength > MaxBodyLength)
        {
            return TypedResults.BadRequest();
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var id = request.Headers[WebhookSignatures.IdHeader].ToString();
        if (!WebhookSignatures.Check(secret, id, request.Headers[WebhookSignatures.TimestampHeader], request.Headers[WebhookSignatures.SignatureHeader], body, clock.GetUtcNow()))
        {
            return TypedResults.Unauthorized();
        }

        // A repeat of an event already received is answered the same, and ignored.
        await store.RecordIncomingAsync(new IncomingWebhookReceived(source, id, body), cancellationToken);
        return TypedResults.NoContent();
    }
}

// Where received events are handled: replace the logging with the app's own work, switching on
// the source (and the event's "type" in the body).
public static class IncomingWebhookHandler
{
    public static void Handle(IncomingWebhookReceived message, ILogger<IncomingWebhookReceived> logger)
        => logger.LogInformation("Received webhook {EventId} from {Source}", message.EventId, message.Source);
}
