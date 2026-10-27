using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Alert
{
    public int AlertId { get; set; }

    public int ServerId { get; set; }

    public int? AlertRuleId { get; set; }

    public string MetricType { get; set; } = null!;

    public double MetricValue { get; set; }

    public double Threshold { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual AlertRule? AlertRule { get; set; }

    public virtual Server Server { get; set; } = null!;
}
