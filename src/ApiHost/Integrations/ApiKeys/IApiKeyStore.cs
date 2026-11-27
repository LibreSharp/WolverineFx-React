namespace ApiHost.Integrations.ApiKeys;

// In Marten or EF Core (ApiKeyStore).
public interface IApiKeyStore
{
    Task<ApiKey?> FindByHashAsync(string secretHash, CancellationToken cancellationToken);

    Task<ApiKey?> FindAsync(Guid organisationId, Guid id, CancellationToken cancellationToken);

    // Newest first, including expired ones (until they're revoked).
    Task<IReadOnlyList<ApiKey>> ListAsync(Guid organisationId, CancellationToken cancellationToken);

    Task SaveAsync(ApiKey key, CancellationToken cancellationToken);

    Task DeleteAsync(ApiKey key, CancellationToken cancellationToken);

    // Records when a key was last used, without touching anything else.
    Task TouchAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken);

    Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken);
}
