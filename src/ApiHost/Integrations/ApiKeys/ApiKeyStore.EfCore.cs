using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Integrations.ApiKeys;

// API keys are an ordinary (not tenanted) table, with a unique index on the hash
// (Data/EfCore/AppDbContext.cs).
public class ApiKeyStore(AppDbContext db) : IApiKeyStore
{
    public Task<ApiKey?> FindByHashAsync(string secretHash, CancellationToken cancellationToken)
        => db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.SecretHash == secretHash, cancellationToken);

    public Task<ApiKey?> FindAsync(Guid organisationId, Guid id, CancellationToken cancellationToken)
        => db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.OrganisationId == organisationId, cancellationToken);

    public async Task<IReadOnlyList<ApiKey>> ListAsync(Guid organisationId, CancellationToken cancellationToken)
        => await db.ApiKeys.AsNoTracking()
            .Where(k => k.OrganisationId == organisationId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task SaveAsync(ApiKey key, CancellationToken cancellationToken)
    {
        if (db.Entry(key).State == EntityState.Detached)
        {
            db.ApiKeys.Add(key);
        }

        return db.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(ApiKey key, CancellationToken cancellationToken)
    {
        db.ApiKeys.Remove(key);
        return db.SaveChangesAsync(cancellationToken);
    }

    // One UPDATE statement, so it never saves anything else the request has changed.
    public Task TouchAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken)
        => db.ApiKeys.Where(k => k.Id == id).ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, at), cancellationToken);

    public Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
        => db.ApiKeys.Where(k => k.OrganisationId == organisationId).ExecuteDeleteAsync(cancellationToken);
}
