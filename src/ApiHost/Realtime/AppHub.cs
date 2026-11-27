#if (Identity)
using ApiHost.Features.Todos;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
#endif
#if (Saas)
using ApiHost.Organisations;
#endif
using Wolverine.SignalR;
using Wolverine.SignalR.Internals;

namespace ApiHost.Realtime;

// The SignalR hub browsers connect to. WolverineHub turns it into a Wolverine transport:
// messages routed with ToSignalR() (or wrapped by ToWebSocketGroup) are pushed to clients
// as CloudEvents JSON on the "ReceiveMessage" method.
#if (Identity)
[Authorize]
#endif
public class AppHub(SignalRTransport endpoint) : WolverineHub(endpoint)
{
    public const string Path = "/api/hub";
#if (Identity)

    public override async Task OnConnectedAsync()
    {
#if (Saas)
        // The organisation in the access token; the browser reconnects when it switches.
        await Groups.AddToGroupAsync(Context.ConnectionId, TodoNotifications.OrganisationGroup(Context.User!.GetOrganisationId().ToString()));
#else
        await Groups.AddToGroupAsync(Context.ConnectionId, TodoNotifications.UserGroup(Context.User!.GetUserId()));
#endif
        await base.OnConnectedAsync();
    }
#endif
}
