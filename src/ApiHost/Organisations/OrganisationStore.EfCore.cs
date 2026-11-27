using ApiHost.Data.EfCore;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace ApiHost.Organisations;

// Organisations and memberships are ordinary (not tenanted) tables; see
// Data/EfCore/AppDbContext.cs for their keys and indexes.
public class OrganisationStore(AppDbContext db, IDbContextOutbox outbox, TimeProvider clock) : IOrganisationStore
{
    public Task<Organisation?> FindAsync(Guid organisationId, CancellationToken cancellationToken)
        => db.Organisations.FirstOrDefaultAsync(o => o.Id == organisationId, cancellationToken);

    public async Task<IReadOnlyList<Membership>> MembershipsOfUserAsync(string userId, CancellationToken cancellationToken)
        => await db.Memberships.Where(m => m.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserOrganisation>> OrganisationsOfUserAsync(string userId, Guid? current, CancellationToken cancellationToken)
    {
        var rows = await (
                from membership in db.Memberships.AsNoTracking()
                join organisation in db.Organisations on membership.OrganisationId equals organisation.Id
                where membership.UserId == userId
                select new { organisation.Id, organisation.Name, membership.Role })
            .ToListAsync(cancellationToken);
        return [.. rows
            .Select(r => new UserOrganisation(r.Id, r.Name, r.Role, r.Id == current))
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public Task<Membership?> FindMembershipAsync(Guid organisationId, string userId, CancellationToken cancellationToken)
        => db.Memberships.FirstOrDefaultAsync(m => m.OrganisationId == organisationId && m.UserId == userId, cancellationToken);

    public Task<bool> CanWorkInAsync(Guid organisationId, string userId, CancellationToken cancellationToken)
        => db.Memberships.AnyAsync(
            m => m.OrganisationId == organisationId && m.UserId == userId
                 && db.Organisations.Any(o => o.Id == organisationId && o.SuspendedAt == null),
            cancellationToken);

    public async Task<IReadOnlyList<Membership>> MembershipsOfOrganisationAsync(Guid organisationId, CancellationToken cancellationToken)
        => await db.Memberships.Where(m => m.OrganisationId == organisationId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OrganisationMember>> MembersAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var rows = await (
                from membership in db.Memberships.AsNoTracking()
                join user in db.Users on membership.UserId equals user.Id
                where membership.OrganisationId == organisationId
                select new OrganisationMember(user.Id, user.Email ?? string.Empty, user.DisplayName, membership.Role, membership.JoinedAt))
            .ToListAsync(cancellationToken);
        return [.. rows.OrderBy(m => m.Email, StringComparer.OrdinalIgnoreCase)];
    }

    public Task<int> CountOwnersAsync(Guid organisationId, CancellationToken cancellationToken)
        => db.Memberships.CountAsync(m => m.OrganisationId == organisationId && m.Role == OrganisationRoles.Owner, cancellationToken);

    public async Task CreateAsync(Organisation organisation, Membership owner, CancellationToken cancellationToken)
    {
        db.Organisations.Add(organisation);
        db.Memberships.Add(owner);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(Organisation organisation, CancellationToken cancellationToken)
        => await db.SaveChangesAsync(cancellationToken);

    public async Task AddMembershipAsync(Membership membership, CancellationToken cancellationToken)
    {
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveMembershipAsync(Membership membership, CancellationToken cancellationToken)
        => await db.SaveChangesAsync(cancellationToken);

    public async Task RemoveMembershipAsync(Membership membership, CancellationToken cancellationToken)
    {
        db.Memberships.Remove(membership);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingInvitation>> InvitationsAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await db.OrganisationInvitations.AsNoTracking()
            .Where(i => i.OrganisationId == organisationId && i.ExpiresAt > now)
            .OrderBy(i => i.Email)
            .Select(i => new PendingInvitation(i.Id, i.Email, i.Role, i.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    public Task<OrganisationInvitation?> FindInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
        => db.OrganisationInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

    public Task<OrganisationInvitation?> FindInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => db.OrganisationInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public async Task AddInvitationAsync(OrganisationInvitation invitation, SendOrganisationInvitation email, CancellationToken cancellationToken)
    {
        // Loaded and removed (not ExecuteDeleteAsync, which would run at once, outside the
        // transaction below).
        db.OrganisationInvitations.RemoveRange(await db.OrganisationInvitations
            .Where(i => i.OrganisationId == invitation.OrganisationId && i.NormalizedEmail == invitation.NormalizedEmail)
            .ToListAsync(cancellationToken));
        db.OrganisationInvitations.Add(invitation);

        // Saves the invitation and the email message in one transaction, then sends the message.
        outbox.Enroll(db);
        await outbox.PublishAsync(email);
        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
    }

    public async Task SaveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken)
        => await db.SaveChangesAsync(cancellationToken);

    public async Task RemoveInvitationAsync(OrganisationInvitation invitation, CancellationToken cancellationToken)
    {
        db.OrganisationInvitations.Remove(invitation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        // Loaded and removed (not ExecuteDeleteAsync, which would run at once, outside the
        // transaction below): an organisation has few memberships and invitations.
        db.OrganisationInvitations.RemoveRange(await db.OrganisationInvitations.Where(i => i.OrganisationId == organisationId).ToListAsync(cancellationToken));
        db.Memberships.RemoveRange(await db.Memberships.Where(m => m.OrganisationId == organisationId).ToListAsync(cancellationToken));
        db.Organisations.RemoveRange(await db.Organisations.Where(o => o.Id == organisationId).ToListAsync(cancellationToken));

        // Saves the removals and the message in one transaction, then sends the message.
        outbox.Enroll(db);
        await outbox.PublishAsync(new OrganisationDeleted(organisationId), new DeliveryOptions { TenantId = organisationId.ToString() });
        await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
    }
}
