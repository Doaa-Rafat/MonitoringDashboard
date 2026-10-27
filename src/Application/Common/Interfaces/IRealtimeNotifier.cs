using Application.Features.Metrics;

namespace Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task MetricCollectedAsync(MetricDto metric, CancellationToken ct = default);
}