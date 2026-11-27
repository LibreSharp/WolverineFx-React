using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace ApiHost.Integrations.Webhooks;

// Subscriptions and deliveries are ordinary (not tenanted) tables (Data/EfCore/AppDbContext.cs).
// The handlers that use it don't take a DbContext themselves, so Wolverine wraps no transaction
// around them: each write here is saved at once, and kept when the handler then fails.
public class WebhookStore(AppDbContext db, IDbContextOutbox outbox, TimeProvider clock) : IWebhookStore
{
    public async Task<IReadOnlyList<WebhookSubscription>> SubscriptionsAsync(Guid organisationId, CancellationToken cancellationToken)
        => await db.WebhookSubscriptions
            .Where(s => s.OrganisationId == organisationId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<WebhookSubscription?> FindSubscriptionAsync(Guid organisationId, Guid id, CancellationToken cancellationToken)
        => db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id && s.OrganisationId == organisationId, cancellationToken);

    public Task SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        if (db.Entry(subscription).State == EntityState.Detached)
        {
            db.WebhookSubscriptions.Add(subscription);
        }

        return db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        await db.WebhookDeliveries.Where(d => d.SubscriptionId == subscription.Id).ExecuteDeleteAsync(cancellationToken);
        db.WebhookSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddDeliveriesAsync(IReadOnlyList<WebhookDelivery> deliveries, Guid organisationId, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        await db.WebhookDeliveries.Where(d => d.OrganisationId == organisationId && d.CreatedAt < olderThan).ExecuteDeleteAsync(cancellationToken);
        db.WebhookDeliveries.AddRange(deliveries);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<WebhookDelivery?> FindDeliveryAsync(Guid id, CancellationToken cancellationToken)
        => db.WebhookDeliveries.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<WebhookDelivery?> FindDeliveryAsync(Guid organisationId, Guid id, CancellationToken cancellationToken)
        => db.WebhookDeliveries.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.OrganisationId == organisationId, cancellationToken);

    public async Task<IReadOnlyList<WebhookDelivery>> DeliveriesAsync(Guid organisationId, Guid subscriptionId, int count, CancellationToken cancellationToken)
        => await db.WebhookDeliveries.AsNoTracking()
            .Where(d => d.OrganisationId == organisationId && d.SubscriptionId == subscriptionId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task RecordAttemptAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
        => db.WebhookDeliveries.Where(d => d.Id == delivery.Id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Attempts, delivery.Attempts)
            .SetProperty(d => d.LastAttemptAt, delivery.LastAttemptAt)
            .SetProperty(d => d.StatusCode, delivery.StatusCode)
            .SetProperty(d => d.Response, delivery.Response)
            .SetProperty(d => d.DeliveredAt, delivery.DeliveredAt), cancellationToken);

    public async Task<bool> RecordIncomingAsync(IncomingWebhookReceived received, CancellationToken cancellationToken)
    {
        var id = $"{received.Source}:{received.EventId}";
        if (await db.IncomingWebhooks.AnyAsync(r => r.Id == id, cancellationToken))
        {
            return false;
        }

        // The primary key refuses a second delivery that got here at the same moment.
        db.IncomingWebhooks.Add(new IncomingWebhookRecord { Id = id, Source = received.Source, ReceivedAt = clock.GetUtcNow() });
        outbox.Enroll(db);
        await outbox.PublishAsync(received);
        try
        {
            await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await db.WebhookDeliveries.Where(d => d.OrganisationId == organisationId).ExecuteDeleteAsync(cancellationToken);
        await db.WebhookSubscriptions.Where(s => s.OrganisationId == organisationId).ExecuteDeleteAsync(cancellationToken);
    }
}
