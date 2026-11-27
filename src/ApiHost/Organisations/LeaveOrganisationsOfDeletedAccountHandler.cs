using ApiHost.Identity;

namespace ApiHost.Organisations;

// When a user deletes their account, they leave each of their organisations. One they were the
// only member of is deleted, with its data (the store publishes OrganisationDeleted for its
// tenant, in the same transaction). The account endpoint refuses first if this would leave an
// organisation without an owner.
public static class LeaveOrganisationsOfDeletedAccountHandler
{
    public static async Task Handle(AccountDeleted message, IOrganisationStore store, CancellationToken cancellationToken)
    {
        foreach (var membership in await store.MembershipsOfUserAsync(message.UserId, cancellationToken))
        {
            // Members whose account still exists: the deleted user is already gone from these.
            var members = await store.MembersAsync(membership.OrganisationId, cancellationToken);
            if (members.All(m => m.UserId == message.UserId))
            {
                await store.DeleteAsync(membership.OrganisationId, cancellationToken);
            }
            else
            {
                await store.RemoveMembershipAsync(membership, cancellationToken);
            }
        }
    }
}
