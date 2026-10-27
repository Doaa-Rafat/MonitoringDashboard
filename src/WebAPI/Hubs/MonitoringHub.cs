using System.Security.Claims;
using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Hubs;

[Authorize]
public class MonitoringHub(
    IPresenceTracker presence,
    IApplicationDbContext db,
    ILogger<MonitoringHub> logger) : Hub<IMonitoringClient>
{
    public override async Task OnConnectedAsync()
    {
        var (userId, userName) = GetIdentity();

        var firstConnection = presence.UserConnected(userId, userName, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Alerts);

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Reports);

        var online = presence.GetOnlineUsers();
        if (firstConnection) await Clients.All.PresenceChanged(online);
        else await Clients.Caller.PresenceChanged(online);

        logger.LogInformation("SignalR connected: user {UserName} ({ConnectionId})", userName, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (userId, userName) = GetIdentity();

        // SignalR removes the connection from all groups by itself.
        if (presence.UserDisconnected(userId, Context.ConnectionId))
            await Clients.All.PresenceChanged(presence.GetOnlineUsers());

        logger.LogInformation("SignalR disconnected: user {UserName} ({ConnectionId})", userName, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public Task SubscribeToDashboard() =>
        Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Dashboard);

    public Task UnsubscribeFromDashboard() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Dashboard);

    public async Task SubscribeToServer(int serverId)
    {
        if (!await db.Servers.AnyAsync(s => s.ServerId == serverId))
            throw new HubException($"Server {serverId} was not found.");

        await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Server(serverId));
    }

    public Task UnsubscribeFromServer(int serverId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.Server(serverId));

    private (int Id, string Name) GetIdentity()
    {
        var user = Context.User;
        if (!int.TryParse(user?.FindFirstValue("sub"), out var id))
            throw new HubException("Unauthorized.");
        return (id, user!.FindFirstValue("unique_name") ?? $"user-{id}");
    }
}