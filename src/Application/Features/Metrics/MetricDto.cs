using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Features.Metrics;

public record MetricDto(int Id, int ServerId, double CpuUsage, double MemoryUsage,
    double DiskUsage, double ResponseTime, string Status, DateTime Timestamp);

public static class MetricMappings
{
    public static readonly Expression<Func<Metric, MetricDto>> ToDto = m => new MetricDto(
        m.MetricId, m.ServerId, m.CpuUsage, m.MemoryUsage, m.DiskUsage,
        m.ResponseTime, m.Status, m.Timestamp);
}