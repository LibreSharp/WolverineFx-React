using ApiHost.Billing;

namespace ApiHost.Organisations;

// The Members limit counts members and the invitations waiting to be accepted, so an organisation
// can't invite more people than its plan has room for.
public class MembersUsage(IOrganisationStore store) : ILimitUsage
{
    public string Limit => PlanLimits.Members;

    public async Task<int> CountAsync(Guid organisationId, CancellationToken cancellationToken)
        => (await store.MembershipsOfOrganisationAsync(organisationId, cancellationToken)).Count
           + (await store.InvitationsAsync(organisationId, cancellationToken)).Count;
}
