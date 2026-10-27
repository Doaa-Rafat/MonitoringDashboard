using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Report
{
    public int ReportId { get; set; }

    public int ServerId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string Status { get; set; } = null!;

    public string? FilePath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Server Server { get; set; } = null!;
}
