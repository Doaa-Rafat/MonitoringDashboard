using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Alerts;

public record AlertEvaluationResult(IReadOnlyList<Alert> Created, IReadOnlyList<Alert> Resolved);

public interface IAlertEvaluator
{
    /// <summary>Adds new alerts and resolves recovered ones on the context. Does NOT save.</summary>
    Task<AlertEvaluationResult> EvaluateAsync(IReadOnlyCollection<Metric> metrics, CancellationToken ct);
}

public class AlertEvaluator(IApplicationDbContext db) : IAlertEvaluator
{
    public async Task<AlertEvaluationResult> EvaluateAsync(IReadOnlyCollection<Metric> metrics, CancellationToken ct)
    {
        var created = new List<Alert>();
        var resolved = new List<Alert>();
        if (metrics.Count == 0) return new(created, resolved);

        var serverIds = metrics.Select(m => m.ServerId).Distinct().ToList();

        var rules = await db.AlertRules.AsNoTracking()
            .Where(r => r.IsEnabled && (r.ServerId == null || serverIds.Contains(r.ServerId.Value)))
            .ToListAsync(ct);

        // "Open" = not resolved yet (Triggered or Acknowledged). Tracked, so we can resolve them below.
        var open = await db.Alerts
            .Where(a => a.Status != AlertStatus.Resolved && a.AlertRuleId != null && serverIds.Contains(a.ServerId))
            .ToListAsync(ct);

        foreach (var metric in metrics)
        {
            if (metric.Status == ServerStatus.Down) continue;   // zeros from a down server are not real readings

            foreach (var rule in rules.Where(r => r.ServerId is null || r.ServerId == metric.ServerId))
            {
                var value = ValueOf(metric, rule.MetricType);
                if (value is null) continue;

                var breached = IsBreached(rule.Operator, value.Value, rule.Threshold);
                var existing = open.FirstOrDefault(a =>
                    a.ServerId == metric.ServerId && a.AlertRuleId == rule.AlertRuleId);

                if (breached && existing is null)
                {
                    var alert = new Alert
                    {
                        ServerId = metric.ServerId,
                        AlertRuleId = rule.AlertRuleId,
                        MetricType = rule.MetricType,
                        MetricValue = value.Value,
                        Threshold = rule.Threshold,
                        Status = AlertStatus.Triggered,
                        CreatedAt = metric.Timestamp
                    };
                    db.Alerts.Add(alert);
                    open.Add(alert);
                    created.Add(alert);
                }
                else if (!breached && existing is not null)
                {
                    existing.Status = AlertStatus.Resolved;
                    existing.ResolvedAt = metric.Timestamp;
                    open.Remove(existing);
                    resolved.Add(existing);
                }
            }
        }

        return new(created, resolved);
    }

    private static double? ValueOf(Metric m, string metricType) => metricType switch
    {
        MetricTypes.Cpu => m.CpuUsage,
        MetricTypes.Memory => m.MemoryUsage,
        MetricTypes.Disk => m.DiskUsage,
        MetricTypes.ResponseTime => m.ResponseTime,
        _ => null
    };

    private static bool IsBreached(string op, double value, double threshold) => op switch
    {
        AlertOperators.GreaterThan => value > threshold,
        AlertOperators.LessThan => value < threshold,
        _ => false
    };
}