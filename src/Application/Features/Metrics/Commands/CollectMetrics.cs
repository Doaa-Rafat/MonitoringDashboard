using Application.Common.Interfaces;
using Application.Features.Alerts;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Metrics.Commands;

public record CollectMetricsCommand : IRequest<int>;

public class CollectMetricsCommandHandler(
    IApplicationDbContext db,
    IMetricsCollector collector,
    IAlertEvaluator alertEvaluator,
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

        // Metrics and alerts share one unit of work: both are saved, or neither.
        var evaluation = await alertEvaluator.EvaluateAsync(collected, ct);
        await db.SaveChangesAsync(ct);

        // Real-time pushes happen AFTER the save and never fail the job: the data is already stored.
        var names = servers.ToDictionary(s => s.ServerId, s => s.Name);

        foreach (var m in collected)
        {
            var dto = new MetricDto(m.MetricId, m.ServerId, m.CpuUsage, m.MemoryUsage,
                m.DiskUsage, m.ResponseTime, m.Status, m.Timestamp);
            await NotifyAsync(() => notifier.MetricCollectedAsync(dto, ct), $"metric of server {m.ServerId}");
        }
        foreach (var a in evaluation.Created)
        {
            var dto = AlertMappings.From(a, names[a.ServerId]);
            await NotifyAsync(() => notifier.AlertTriggeredAsync(dto, ct), $"alert {a.AlertId}");
        }
        foreach (var a in evaluation.Resolved)
        {
            var dto = AlertMappings.From(a, names[a.ServerId]);
            await NotifyAsync(() => notifier.AlertUpdatedAsync(dto, ct), $"alert {a.AlertId}");
        }

        if (evaluation.Created.Count > 0 || evaluation.Resolved.Count > 0)
            logger.LogInformation("Alerts: {Created} triggered, {Resolved} auto-resolved",
                evaluation.Created.Count, evaluation.Resolved.Count);

        return collected.Count;
    }

    private async Task NotifyAsync(Func<Task> send, string what)
    {
        try { await send(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Real-time notification failed: {What}", what);
        }
    }
}