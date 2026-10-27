using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Metric
{
    public int MetricId { get; set; }

    public int ServerId { get; set; }

    public double CpuUsage { get; set; }

    public double MemoryUsage { get; set; }

    public double DiskUsage { get; set; }

    public double ResponseTime { get; set; }

    public string Status { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public virtual Server Server { get; set; } = null!;
}
