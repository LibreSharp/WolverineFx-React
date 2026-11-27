using ApiHost.DataExport;

namespace ApiHost.Organisations;

// The organisations the user belongs to, in "Download your data".
public class MembershipsPersonalData(IOrganisationStore store) : IPersonalDataSource
{
    public string Section => "organisations";

    public async Task<object?> ExportAsync(string userId, CancellationToken cancellationToken)
        => (await store.OrganisationsOfUserAsync(userId, null, cancellationToken)).Select(o => new { o.Id, o.Name, o.Role });
}

// The organisation itself, its members and pending invitations, in its data download.
public class OrganisationDetailsData(IOrganisationStore store) : IOrganisationDataSource
{
    public string Section => "organisation";

    public async Task<object?> ExportAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        var organisation = await store.FindAsync(organisationId, cancellationToken);
        return organisation is null
            ? null
            : new
            {
                organisation.Id,
                organisation.Name,
                organisation.CreatedAt,
                Members = await store.MembersAsync(organisationId, cancellationToken),
                Invitations = await store.InvitationsAsync(organisationId, cancellationToken),
            };
    }
}
