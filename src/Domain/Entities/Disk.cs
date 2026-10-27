using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Disk
{
    public int DiskId { get; set; }

    public int ServerId { get; set; }

    public string DriveLetter { get; set; } = null!;

    public long FreeSpaceMb { get; set; }

    public long TotalSpaceMb { get; set; }

    public double UsedPercentage { get; set; }

    public DateTime Timestamp { get; set; }

    public virtual Server Server { get; set; } = null!;
}
