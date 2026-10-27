using Application.Features.Metrics;

namespace WebAPI.Hubs;

public record OnlineUserDto(int UserId, string UserName, int Connections);

public interface IMonitoringClient
{
    Task MetricReceived(MetricDto metric);
    Task PresenceChanged(IReadOnlyList<OnlineUserDto> users);
    // step 7: Task AlertTriggered(AlertDto alert);
}