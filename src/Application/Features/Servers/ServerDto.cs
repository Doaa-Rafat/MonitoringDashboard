using System.Linq.Expressions;
using Domain.Entities;

namespace Application.Features.Servers;

public record LatestMetricDto(double CpuUsage, double MemoryUsage, double DiskUsage,
    double ResponseTime, DateTime Timestamp, double? NetworkInKbps = null, double? NetworkOutKbps = null);

public record ServerDto(int Id, string Name, string? IpAddress, string Status,
    string? Description, DateTime CreatedAt, LatestMetricDto? LatestMetric);

public static class ServerMappings
{
    // One projection reused by every query: EF turns it into a single SQL statement.
    public static readonly Expression<Func<Server, ServerDto>> ToDto = s => new ServerDto(
        s.ServerId, s.Name, s.Ipaddress, s.Status, s.Description, s.CreatedAt,
        s.Metrics.OrderByDescending(m => m.Timestamp)
            .Select(m => new LatestMetricDto(m.CpuUsage, m.MemoryUsage, m.DiskUsage, m.ResponseTime,
                m.Timestamp, m.NetworkInKbps, m.NetworkOutKbps))
            .FirstOrDefault());
}
