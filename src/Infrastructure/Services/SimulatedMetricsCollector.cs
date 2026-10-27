using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Constants;

namespace Infrastructure.Services;

/// <summary>Generates plausible random metrics, with occasional spikes that trigger alert rules.</summary>
public class SimulatedMetricsCollector : IMetricsCollector
{
    public Task<MetricSample> CollectAsync(int serverId, string serverName, CancellationToken ct)
    {
        var rnd = Random.Shared;

        if (rnd.NextDouble() < 0.02)   // ~2%: the server is down
            return Task.FromResult(new MetricSample(0, 0, 0, 0, ServerStatus.Down));

        var sample = new MetricSample(
            CpuUsage:     rnd.NextDouble() < 0.08 ? Between(85, 99)  : Between(10, 70),
            MemoryUsage:  rnd.NextDouble() < 0.05 ? Between(91, 99)  : Between(30, 85),
            DiskUsage:    rnd.NextDouble() < 0.02 ? Between(91, 98)  : Between(40, 80),
            ResponseTime: rnd.NextDouble() < 0.05 ? Between(1000, 2500) : Between(50, 400),
            Status:       ServerStatus.Up);

        return Task.FromResult(sample);
    }

    private static double Between(double min, double max) =>
        Math.Round(min + Random.Shared.NextDouble() * (max - min), 1);
}