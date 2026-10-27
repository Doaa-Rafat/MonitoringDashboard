using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class AlertRule
{
    public int AlertRuleId { get; set; }

    public int? ServerId { get; set; }

    public string MetricType { get; set; } = null!;

    public string Operator { get; set; } = null!;

    public double Threshold { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public virtual Server? Server { get; set; }
}
