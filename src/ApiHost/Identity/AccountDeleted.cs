namespace ApiHost.Identity;

// Published when a user deletes their account, after the user has been removed. Each feature
// that stores data about users handles it to delete that data (see
// Features/Todos/DeleteTodosOfDeletedAccountHandler.cs), so the Identity code needs no knowledge of
// those features. It goes through Wolverine's durable outbox, so the clean-up survives a restart.
public record AccountDeleted(string UserId);
