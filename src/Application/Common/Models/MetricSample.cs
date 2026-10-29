namespace Application.Common.Models;

public record MetricSample(double CpuUsage, double MemoryUsage, double DiskUsage,
    double ResponseTime, string Status, double NetworkInKbps, double NetworkOutKbps);
