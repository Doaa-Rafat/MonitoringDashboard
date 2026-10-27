using Application.Common.Interfaces;
using Application.Features.Metrics;

namespace Infrastructure.Services;

// Placeholder until the SignalR notifier replaces it (step 6).
public class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task MetricCollectedAsync(MetricDto metric, CancellationToken ct = default) => Task.CompletedTask;
}