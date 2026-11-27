using ApiHost.Identity;
using Wolverine;

namespace ApiHost.Organisations;

// When a user deletes their account, they leave each of their organisations. One they were the
// only member of is deleted, with its data (OrganisationDeleted, sent for its tenant). The
// account endpoint refuses first if this would leave an organisation without an owner.
public static class LeaveOrganisationsOfDeletedAccountHandler
{
    public static async Task<OutgoingMessages> Handle(AccountDeleted message, IOrganisationStore store, CancellationToken cancellationToken)
    {
        var messages = new OutgoingMessages();
        foreach (var membership in await store.MembershipsOfUserAsync(message.UserId, cancellationToken))
        {
            var members = await store.MembersAsync(membership.OrganisationId, cancellationToken);
            if (members.All(m => m.UserId == message.UserId))
            {
                await store.DeleteAsync(membership.OrganisationId, cancellationToken);
                messages.Add(new OrganisationDeleted(membership.OrganisationId).WithTenantId(membership.OrganisationId.ToString()));
            }
            else
            {
                await store.RemoveMembershipAsync(membership, cancellationToken);
            }
        }

        return messages;
    }
}
