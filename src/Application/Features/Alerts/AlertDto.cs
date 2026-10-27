using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Features.Alerts;

public record AlertDto(int Id, int ServerId, string ServerName, int? AlertRuleId, string MetricType,
    double MetricValue, double Threshold, string Status, DateTime CreatedAt, DateTime? ResolvedAt);

public record AlertRuleDto(int Id, int? ServerId, string? ServerName, string MetricType, string Operator,
    double Threshold, bool IsEnabled, DateTime CreatedAt);

public static class AlertMappings
{
    // For queries: translated to SQL.
    public static readonly Expression<Func<Alert, AlertDto>> Projection = a => new AlertDto(
        a.AlertId, a.ServerId, a.Server.Name, a.AlertRuleId, a.MetricType,
        a.MetricValue, a.Threshold, a.Status, a.CreatedAt, a.ResolvedAt);

    // For in-memory entities we already have (the server name is known).
    public static AlertDto From(Alert a, string serverName) => new(
        a.AlertId, a.ServerId, serverName, a.AlertRuleId, a.MetricType,
        a.MetricValue, a.Threshold, a.Status, a.CreatedAt, a.ResolvedAt);
}

public static class AlertRuleMappings
{
    public static readonly Expression<Func<AlertRule, AlertRuleDto>> ToDto = r => new AlertRuleDto(
        r.AlertRuleId, r.ServerId, r.Server == null ? null : r.Server.Name,
        r.MetricType, r.Operator, r.Threshold, r.IsEnabled, r.CreatedAt);
}