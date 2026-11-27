using Marten;

namespace ApiHost.Integrations.Secrets;

// Secrets are ordinary (not tenanted) Marten documents, with encrypted values.
public class SecretStore(IDocumentSession session, IDocumentStore store) : ISecretStore
{
    public Task<OrganisationSecret?> FindAsync(Guid organisationId, string name, CancellationToken cancellationToken)
        => session.LoadAsync<OrganisationSecret>(OrganisationSecret.IdFor(organisationId, name), cancellationToken);

    public async Task<IReadOnlyList<OrganisationSecret>> ListAsync(Guid organisationId, CancellationToken cancellationToken)
        => await session.Query<OrganisationSecret>()
            .Where(s => s.OrganisationId == organisationId)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public Task SaveAsync(OrganisationSecret secret, CancellationToken cancellationToken)
    {
        session.Store(secret);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(OrganisationSecret secret, CancellationToken cancellationToken)
    {
        session.Delete(secret);
        return session.SaveChangesAsync(cancellationToken);
    }

    // Its own session: the OrganisationDeleted handler deletes several features' data, and a
    // handler's session can only be saved once.
    public async Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await using var delete = store.LightweightSession();
        delete.DeleteWhere<OrganisationSecret>(s => s.OrganisationId == organisationId);
        await delete.SaveChangesAsync(cancellationToken);
    }
}
