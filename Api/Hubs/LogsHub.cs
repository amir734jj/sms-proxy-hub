using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shared;

namespace Api.Hubs;

// Streams application (Serilog) and SMS Gate logs to connected admins in real time.
[Authorize(Roles = Roles.Admin)]
public sealed class LogsHub : Hub
{
    public const string GroupName = "admin-logs";

    private static int _clientCount;

    // Used by the Serilog sink and the SMS Gate poller to avoid work when nobody is watching.
    public static int ClientCount => Volatile.Read(ref _clientCount);

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        Interlocked.Increment(ref _clientCount);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _clientCount);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);
        await base.OnDisconnectedAsync(exception);
    }
}
