using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Metrics.Commands;

public record CollectMetricsCommand : IRequest<int>;

public class CollectMetricsCommandHandler(
    IApplicationDbContext db,
    IMetricsCollector collector,
    IRealtimeNotifier notifier,
    ILogger<CollectMetricsCommandHandler> logger) : IRequestHandler<CollectMetricsCommand, int>
{
    public async Task<int> Handle(CollectMetricsCommand request, CancellationToken ct)
    {
        var servers = await db.Servers.ToListAsync(ct);
        var timestamp = DateTime.UtcNow;                 // one timestamp per cycle
        var collected = new List<Metric>();

        foreach (var server in servers)
        {
            try
            {
                var sample = await collector.CollectAsync(server.ServerId, server.Name, ct);
                var metric = new Metric
                {
                    ServerId = server.ServerId,
                    CpuUsage = sample.CpuUsage,
                    MemoryUsage = sample.MemoryUsage,
                    DiskUsage = sample.DiskUsage,
                    ResponseTime = sample.ResponseTime,
                    Status = sample.Status,
                    Timestamp = timestamp
                };
                server.Status = sample.Status;
                db.Metrics.Add(metric);
                collected.Add(metric);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failing server must not stop the others.
                logger.LogError(ex, "Metric collection failed for server {ServerId}", server.ServerId);
            }
        }

        await db.SaveChangesAsync(ct);

        // TODO (step 7): evaluate alert rules here, after the metrics are saved.

        // Real-time push happens AFTER the save and never fails the job: the data is already stored.
        foreach (var m in collected)
        {
            try
            {
                await notifier.MetricCollectedAsync(new MetricDto(m.MetricId, m.ServerId, m.CpuUsage,
                    m.MemoryUsage, m.DiskUsage, m.ResponseTime, m.Status, m.Timestamp), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Real-time notification failed for server {ServerId}", m.ServerId);
            }
        }

        return collected.Count;
    }
}