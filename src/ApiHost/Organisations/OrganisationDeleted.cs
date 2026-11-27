namespace ApiHost.Organisations;

// Published when an organisation is deleted, for the tenant of that organisation, in the same
// transaction as the deletion (IOrganisationStore.DeleteAsync), so each feature's handler deletes
// its own data for it: see
// Features/Todos/DeleteTodosOfDeletedOrganisationHandler.cs.
public record OrganisationDeleted(Guid OrganisationId);
