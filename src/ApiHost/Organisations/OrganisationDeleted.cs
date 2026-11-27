namespace ApiHost.Organisations;

// Published when an organisation is deleted, for the tenant of that organisation
// (WithTenantId), so each feature's handler deletes its own data for it: see
// Features/Todos/DeleteTodosOfDeletedOrganisationHandler.cs.
public record OrganisationDeleted(Guid OrganisationId);
