namespace ApiHost.Integrations.Webhooks;

// In Marten or EF Core (WebhookStore).
public interface IWebhookStore
{
    Task<IReadOnlyList<WebhookSubscription>> SubscriptionsAsync(Guid organisationId, CancellationToken cancellationToken);

    Task<WebhookSubscription?> FindSubscriptionAsync(Guid organisationId, Guid id, CancellationToken cancellationToken);

    Task SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken);

    // With its deliveries.
    Task DeleteSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken);

    // Saves new deliveries, and deletes the organisation's deliveries made before olderThan.
    Task AddDeliveriesAsync(IReadOnlyList<WebhookDelivery> deliveries, Guid organisationId, DateTimeOffset olderThan, CancellationToken cancellationToken);

    Task<WebhookDelivery?> FindDeliveryAsync(Guid id, CancellationToken cancellationToken);

    Task<WebhookDelivery?> FindDeliveryAsync(Guid organisationId, Guid id, CancellationToken cancellationToken);

    // Newest first.
    Task<IReadOnlyList<WebhookDelivery>> DeliveriesAsync(Guid organisationId, Guid subscriptionId, int count, CancellationToken cancellationToken);

    // Records an attempt. Saved on its own, so it's kept when the handler then fails to be retried.
    Task RecordAttemptAsync(WebhookDelivery delivery, CancellationToken cancellationToken);

    Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken);

    // Stores a received event and publishes it for its handler, in one transaction. False if that
    // event was received before (it's then ignored).
    Task<bool> RecordIncomingAsync(IncomingWebhookReceived received, CancellationToken cancellationToken);
}
