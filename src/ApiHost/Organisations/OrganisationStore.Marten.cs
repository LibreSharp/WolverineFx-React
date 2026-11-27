using ApiHost.Identity;
using Marten;
using Wolverine;
using Wolverine.Marten;

namespace ApiHost.Organisations;

// Organisations and memberships are ordinary (not tenanted) Marten documents; see
// Data/Marten/MartenPersistence.cs for their indexes.
public class OrganisationStore(IDocumentSession session, IMartenOutbox outbox) : IOrganisationStore
{
    public Task<Organisation?> FindAsync(Guid organisationId, CancellationToken cancellationToken)
        => session.LoadAsync<Organisation>(organisationId, cancellationToken);

    public Task<IReadOnlyList<Membership>> MembershipsOfUserAsync(string userId, CancellationToken cancellationToken)
        => session.Query<Membership>().Where(m => m.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserOrganisation>> OrganisationsOfUserAsync(string userId, Guid? current, CancellationToken cancellationToken)
    {
        var memberships = await MembershipsOfUserAsync(userId, cancellationToken);
        var organisations = (await session.LoadManyAsync<Organisation>(cancellationToken, memberships.Select(m => m.OrganisationId)))
            .ToDictionary(o => o.Id);
        return [.. memberships
            .Where(m => organisations.ContainsKey(m.OrganisationId))
            .Select(m => new UserOrganisation(m.OrganisationId, organisations[m.OrganisationId].Name, m.Role, m.OrganisationId == current))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public Task<Membership?> FindMembershipAsync(Guid organisationId, string userId, CancellationToken cancellationToken)
        => session.Query<Membership>().FirstOrDefaultAsync(m => m.OrganisationId == organisationId && m.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<OrganisationMember>> MembersAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var memberships = await session.Query<Membership>().Where(m => m.OrganisationId == organisationId).ToListAsync(cancellationToken);
        var users = (await session.LoadManyAsync<ApplicationUser>(cancellationToken, memberships.Select(m => m.UserId)))
            .ToDictionary(u => u.Id, StringComparer.Ordinal);
        return [.. memberships
            .Where(m => users.ContainsKey(m.UserId))
            .Select(m => new OrganisationMember(m.UserId, users[m.UserId].Email ?? string.Empty, users[m.UserId].DisplayName, m.Role, m.JoinedAt))
            .OrderBy(m => m.Email, StringComparer.OrdinalIgnoreCase)];
    }

    public Task<int> CountOwnersAsync(Guid organisationId, CancellationToken cancellationToken)
        => session.Query<Membership>().CountAsync(m => m.OrganisationId == organisationId && m.Role == OrganisationRoles.Owner, cancellationToken);

    public Task CreateAsync(Organisation organisation, Membership owner, CancellationToken cancellationToken)
    {
        session.Store(organisation);
        session.Store(owner);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(Organisation organisation, CancellationToken cancellationToken)
    {
        session.Store(organisation);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task AddMembershipAsync(Membership membership, CancellationToken cancellationToken)
        => SaveMembershipAsync(membership, cancellationToken);

    public Task SaveMembershipAsync(Membership membership, CancellationToken cancellationToken)
    {
        session.Store(membership);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task RemoveMembershipAsync(Membership membership, CancellationToken cancellationToken)
    {
        session.Delete(membership);
        return session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingInvitation>> InvitationsAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var invitations = await session.Query<OrganisationInvitation>()
            .Where(i => i.OrganisationId == organisationId && i.ExpiresAt > now)
            .OrderBy(i => i.Email)
            .ToListAsync(cancellationToken);
        return [.. invitations.Select(i => new PendingInvitation(i.Id, i.Email, i.Role, i.ExpiresAt))];
    }

    public Task<OrganisationInvitation?> FindInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
        => session.LoadAsync<OrganisationInvitation>(invitationId, cancellationToken);

    public Task<OrganisationInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => session.Query<OrganisationInvitation>().FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task AddInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken)
    {
        session.DeleteWhere<OrganisationInvitation>(i => i.OrganisationId == invitation.OrganisationId && i.NormalizedEmail == invitation.NormalizedEmail);
        session.Store(invitation);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task SaveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken)
    {
        session.Store(invitation);
        return session.SaveChangesAsync(cancellationToken);
    }

    public Task RemoveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken)
    {
        session.Delete(invitation);
        return session.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        session.DeleteWhere<OrganisationInvitation>(i => i.OrganisationId == organisationId);
        session.DeleteWhere<Membership>(m => m.OrganisationId == organisationId);
        session.Delete<Organisation>(organisationId);

        // The message is stored by this session's SaveChangesAsync, in the same transaction, and
        // sent once it commits.
        outbox.Enroll(session);
        await outbox.PublishAsync(new OrganisationDeleted(organisationId), new DeliveryOptions { TenantId = organisationId.ToString() });
        await session.SaveChangesAsync(cancellationToken);
    }
}
