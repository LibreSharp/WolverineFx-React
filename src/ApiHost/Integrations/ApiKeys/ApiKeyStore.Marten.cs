using Marten;
using Marten.Patching;

namespace ApiHost.Integrations.ApiKeys;

// API keys are ordinary (not tenanted) Marten documents, indexed by hash (Data/Marten/MartenPersistence.cs).
public class ApiKeyStore(IDocumentSession session, IDocumentStore store) : IApiKeyStore
{
    public Task<ApiKey?> FindByHashAsync(string secretHash, CancellationToken cancellationToken)
        => session.Query<ApiKey>().FirstOrDefaultAsync(k => k.SecretHash == secretHash, cancellationToken);

    public async Task<ApiKey?> FindAsync(Guid organisationId, Guid id, CancellationToken cancellationToken)
    {
        var key = await session.LoadAsync<ApiKey>(id, cancellationToken);
        return key?.OrganisationId == organisationId ? key : null;
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(Guid organisationId, CancellationToken cancellationToken)
        => await session.Query<ApiKey>()
            .Where(k => k.OrganisationId == organisationId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task SaveAsync(ApiKey key, CancellationToken cancellationToken)
    {
        session.Store(key);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(ApiKey key, CancellationToken cancellationToken)
    {
        session.Delete(key);
        return session.SaveChangesAsync(cancellationToken);
    }

    // In a session of its own, so it never saves anything else the request has changed.
    public async Task TouchAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var touch = store.LightweightSession();
        touch.Patch<ApiKey>(id).Set(k => k.LastUsedAt, at);
        await touch.SaveChangesAsync(cancellationToken);
    }

    // Its own session too: the OrganisationDeleted handler deletes several features' data, and a
    // handler's session can only be saved once.
    public async Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await using var delete = store.LightweightSession();
        delete.DeleteWhere<ApiKey>(k => k.OrganisationId == organisationId);
        await delete.SaveChangesAsync(cancellationToken);
    }
}
