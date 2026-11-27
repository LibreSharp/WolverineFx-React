using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Integrations.Secrets;

// Secrets are an ordinary (not tenanted) table, with encrypted values (Data/EfCore/AppDbContext.cs).
public class SecretStore(AppDbContext db) : ISecretStore
{
    public Task<OrganisationSecret?> FindAsync(Guid organisationId, string name, CancellationToken cancellationToken)
    {
        var id = OrganisationSecret.IdFor(organisationId, name);
        return db.OrganisationSecrets.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganisationSecret>> ListAsync(Guid organisationId, CancellationToken cancellationToken)
        => await db.OrganisationSecrets.AsNoTracking()
            .Where(s => s.OrganisationId == organisationId)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public Task SaveAsync(OrganisationSecret secret, CancellationToken cancellationToken)
    {
        if (db.Entry(secret).State == EntityState.Detached)
        {
            db.OrganisationSecrets.Add(secret);
        }

        return db.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(OrganisationSecret secret, CancellationToken cancellationToken)
    {
        db.OrganisationSecrets.Remove(secret);
        return db.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
        => db.OrganisationSecrets.Where(s => s.OrganisationId == organisationId).ExecuteDeleteAsync(cancellationToken);
}
