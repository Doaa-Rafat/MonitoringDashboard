using Application.Features.Alerts;
using Application.Features.Metrics;

namespace Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task MetricCollectedAsync(MetricDto metric, CancellationToken ct = default);
    Task AlertTriggeredAsync(AlertDto alert, CancellationToken ct = default);
    Task AlertUpdatedAsync(AlertDto alert, CancellationToken ct = default); 
}