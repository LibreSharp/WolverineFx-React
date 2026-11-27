using Wolverine.Attributes;
#if (Identity)
using Wolverine.SignalR;
#endif

namespace ApiHost.Features.Todos;

// Pushed to browsers over SignalR (see Realtime/AppHub.cs) whenever a Todo is created or
// completed, so every open tab refreshes its list. The client matches on the stable
// "todo-changed" name (the CloudEvents "type"), not on the .NET type name.
[MessageIdentity("todo-changed")]
public record TodoChanged(Guid TodoId);

public static class TodoNotifications
{
#if (Saas)
    // Each SignalR connection joins its organisation's group and its user's group
    // (AppHub.OnConnectedAsync). A change reaches every open tab of that organisation's members
    // and nobody else's; a reminder reaches only the tabs of the member who asked for it.
    public static string OrganisationGroup(string organisationId) => $"organisation:{organisationId}";

    public static string UserGroup(string userId) => $"user:{userId}";

    public static object Changed(Guid todoId, string organisationId)
        => new TodoChanged(todoId).ToWebSocketGroup(OrganisationGroup(organisationId));

    public static object Reminder(Todo todo, string userId)
        => new TodoReminder(todo.Id, todo.Title).ToWebSocketGroup(UserGroup(userId));
#elif (Identity)
    // Each SignalR connection joins its user's group (AppHub.OnConnectedAsync), so a
    // notification reaches all of that user's tabs and nobody else's.
    public static string UserGroup(string userId) => $"user:{userId}";

    public static object Changed(Guid todoId, string ownerId)
        => new TodoChanged(todoId).ToWebSocketGroup(UserGroup(ownerId));

    public static object Reminder(Todo todo)
        => new TodoReminder(todo.Id, todo.Title).ToWebSocketGroup(UserGroup(todo.OwnerId));
#else
    // Sent to every connected browser, by the ToSignalR() routing rule in Program.cs.
    public static object Changed(Guid todoId) => new TodoChanged(todoId);

    public static object Reminder(Todo todo) => new TodoReminder(todo.Id, todo.Title);
#endif
}
