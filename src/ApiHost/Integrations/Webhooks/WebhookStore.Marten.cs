using JasperFx;
using Marten;
using Wolverine.Marten;

namespace ApiHost.Integrations.Webhooks;

// Subscriptions and deliveries are ordinary (not tenanted) Marten documents. Writes that happen in
// message handlers use sessions of their own, as a handler's session can only be saved once.
public class WebhookStore(IDocumentSession session, IDocumentStore store, IMartenOutbox outbox, TimeProvider clock) : IWebhookStore
{
    public async Task<IReadOnlyList<WebhookSubscription>> SubscriptionsAsync(Guid organisationId, CancellationToken cancellationToken)
        => await session.Query<WebhookSubscription>()
            .Where(s => s.OrganisationId == organisationId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<WebhookSubscription?> FindSubscriptionAsync(Guid organisationId, Guid id, CancellationToken cancellationToken)
    {
        var subscription = await session.LoadAsync<WebhookSubscription>(id, cancellationToken);
        return subscription?.OrganisationId == organisationId ? subscription : null;
    }

    public Task SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        session.Store(subscription);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        session.Delete(subscription);
        session.DeleteWhere<WebhookDelivery>(d => d.SubscriptionId == subscription.Id);
        return session.SaveChangesAsync(cancellationToken);
    }

    public async Task AddDeliveriesAsync(IReadOnlyList<WebhookDelivery> deliveries, Guid organisationId, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        await using var write = store.LightweightSession();
        write.DeleteWhere<WebhookDelivery>(d => d.OrganisationId == organisationId && d.CreatedAt < olderThan);
        write.Store(deliveries.ToArray());
        await write.SaveChangesAsync(cancellationToken);
    }

    public Task<WebhookDelivery?> FindDeliveryAsync(Guid id, CancellationToken cancellationToken)
        => session.LoadAsync<WebhookDelivery>(id, cancellationToken);

    public async Task<WebhookDelivery?> FindDeliveryAsync(Guid organisationId, Guid id, CancellationToken cancellationToken)
    {
        var delivery = await session.LoadAsync<WebhookDelivery>(id, cancellationToken);
        return delivery?.OrganisationId == organisationId ? delivery : null;
    }

    public async Task<IReadOnlyList<WebhookDelivery>> DeliveriesAsync(Guid organisationId, Guid subscriptionId, int count, CancellationToken cancellationToken)
        => await session.Query<WebhookDelivery>()
            .Where(d => d.OrganisationId == organisationId && d.SubscriptionId == subscriptionId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task RecordAttemptAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        await using var write = store.LightweightSession();
        write.Store(delivery);
        await write.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RecordIncomingAsync(IncomingWebhookReceived received, CancellationToken cancellationToken)
    {
        var id = $"{received.Source}:{received.EventId}";
        if (await session.LoadAsync<IncomingWebhookRecord>(id, cancellationToken) is not null)
        {
            return false;
        }

        // Insert fails if another delivery of the event got here first.
        session.Insert(new IncomingWebhookRecord { Id = id, Source = received.Source, ReceivedAt = clock.GetUtcNow() });
        outbox.Enroll(session);
        await outbox.PublishAsync(received);
        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DocumentAlreadyExistsException)
        {
            return false;
        }
    }

    public async Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await using var delete = store.LightweightSession();
        delete.DeleteWhere<WebhookDelivery>(d => d.OrganisationId == organisationId);
        delete.DeleteWhere<WebhookSubscription>(s => s.OrganisationId == organisationId);
        await delete.SaveChangesAsync(cancellationToken);
    }
}
