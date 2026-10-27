using Application.Common.Interfaces;
using Application.Features.Alerts;
using Application.Features.Metrics;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Hubs;

namespace WebAPI.Services;

public class SignalRRealtimeNotifier(IHubContext<MonitoringHub, IMonitoringClient> hub) : IRealtimeNotifier
{
    public async Task MetricCollectedAsync(MetricDto metric, CancellationToken ct = default)
    {
        await hub.Clients.Group(HubGroups.Server(metric.ServerId)).MetricReceived(metric);
        await hub.Clients.Group(HubGroups.Dashboard).MetricReceived(metric);
    }
    public Task AlertTriggeredAsync(AlertDto alert, CancellationToken ct = default) =>
    hub.Clients.Group(HubGroups.Alerts).AlertTriggered(alert);

    public Task AlertUpdatedAsync(AlertDto alert, CancellationToken ct = default) =>
    hub.Clients.Group(HubGroups.Alerts).AlertUpdated(alert);
}