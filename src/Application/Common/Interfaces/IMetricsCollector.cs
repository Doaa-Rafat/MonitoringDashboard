using Application.Common.Models;

namespace Application.Common.Interfaces;

public interface IMetricsCollector
{
    Task<MetricSample> CollectAsync(int serverId, string serverName, CancellationToken ct);
}