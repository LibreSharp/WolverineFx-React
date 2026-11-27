using ApiHost.Identity;

namespace ApiHost.Organisations;

// When a user deletes their account, they leave each of their organisations. One they were the
// only member of is deleted, with its data (the store publishes OrganisationDeleted for its
// tenant, in the same transaction).
public static class LeaveOrganisationsOfDeletedAccountHandler
{
    public static async Task Handle(AccountDeleted message, IOrganisationStore store, CancellationToken cancellationToken)
    {
        foreach (var membership in await store.MembershipsOfUserAsync(message.UserId, cancellationToken))
        {
            var others = (await store.MembershipsOfOrganisationAsync(membership.OrganisationId, cancellationToken))
                .Where(m => m.UserId != message.UserId)
                .ToList();
            if (others.Count == 0)
            {
                await store.DeleteAsync(membership.OrganisationId, cancellationToken);
                continue;
            }

            await store.RemoveMembershipAsync(membership, cancellationToken);

            // The account endpoint refuses while the user is an organisation's only owner and others
            // are members, but someone may have joined since it checked. Rather than leave the
            // organisation without an owner, make its longest-standing admin (or member) one.
            if (membership.Role == OrganisationRoles.Owner && others.All(m => m.Role != OrganisationRoles.Owner))
            {
                var successor = others
                    .OrderBy(m => m.Role == OrganisationRoles.Admin ? 0 : 1)
                    .ThenBy(m => m.JoinedAt)
                    .First();
                successor.Role = OrganisationRoles.Owner;
                await store.SaveMembershipAsync(successor, cancellationToken);
            }
        }
    }
}
