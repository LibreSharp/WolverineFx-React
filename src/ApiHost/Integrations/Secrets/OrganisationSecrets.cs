using Microsoft.AspNetCore.DataProtection;

namespace ApiHost.Integrations.Secrets;

// A value an organisation gives the app to keep secret, such as its own API key for another
// service or an OAuth token: stored encrypted, never shown again, and read only by the app's code
// (OrganisationSecrets.GetAsync). Not tenanted: it names its organisation.
public class OrganisationSecret
{
    // "{OrganisationId}:{Name}".
    public string Id { get; set; } = string.Empty;

    public Guid OrganisationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ProtectedValue { get; set; } = string.Empty;

    // The value's last four characters, so people can tell which one is set.
    public string Hint { get; set; } = string.Empty;

    public string UpdatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public static string IdFor(Guid organisationId, string name) => $"{organisationId}:{name}";
}

// What the organisation page shows of a secret: never its value.
public record SecretSummary(string Name, string Hint, DateTimeOffset UpdatedAt);

// Encrypts with ASP.NET Core Data Protection, whose keys the app keeps in PostgreSQL (shared by every
// instance and kept across restarts). Each value is encrypted for one purpose and one owner (an
// organisation, a webhook), so a value copied into another row doesn't decrypt.
public class SecretProtection(IDataProtectionProvider provider)
{
    public string Protect(string purpose, string owner, string value) => provider.CreateProtector(purpose, owner).Protect(value);

    public string Unprotect(string purpose, string owner, string protectedValue) => provider.CreateProtector(purpose, owner).Unprotect(protectedValue);
}

// In Marten or EF Core (SecretStore).
public interface ISecretStore
{
    Task<OrganisationSecret?> FindAsync(Guid organisationId, string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganisationSecret>> ListAsync(Guid organisationId, CancellationToken cancellationToken);

    Task SaveAsync(OrganisationSecret secret, CancellationToken cancellationToken);

    Task DeleteAsync(OrganisationSecret secret, CancellationToken cancellationToken);

    Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken);
}

// An organisation's secrets, for the app's own code: for example
// `await secrets.GetAsync(organisationId, "openai-api-key", cancellationToken)` before calling that
// service for the organisation. Names are lower case letters, digits and dashes.
public class OrganisationSecrets(ISecretStore store, SecretProtection protection, TimeProvider clock)
{
    private const string Purpose = "OrganisationSecrets";

    public async Task<string?> GetAsync(Guid organisationId, string name, CancellationToken cancellationToken)
        => await store.FindAsync(organisationId, name, cancellationToken) is { } secret
            ? protection.Unprotect(Purpose, organisationId.ToString(), secret.ProtectedValue)
            : null;

    public async Task<IReadOnlyList<SecretSummary>> ListAsync(Guid organisationId, CancellationToken cancellationToken)
        => [.. (await store.ListAsync(organisationId, cancellationToken)).Select(s => new SecretSummary(s.Name, s.Hint, s.UpdatedAt))];

    public async Task SetAsync(Guid organisationId, string name, string value, string userId, CancellationToken cancellationToken)
    {
        var secret = await store.FindAsync(organisationId, name, cancellationToken)
            ?? new OrganisationSecret { Id = OrganisationSecret.IdFor(organisationId, name), OrganisationId = organisationId, Name = name };
        secret.ProtectedValue = protection.Protect(Purpose, organisationId.ToString(), value);
        secret.Hint = value.Length > 8 ? value[^4..] : string.Empty;
        secret.UpdatedByUserId = userId;
        secret.UpdatedAt = clock.GetUtcNow();
        await store.SaveAsync(secret, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid organisationId, string name, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(organisationId, name, cancellationToken) is not { } secret)
        {
            return false;
        }

        await store.DeleteAsync(secret, cancellationToken);
        return true;
    }
}
