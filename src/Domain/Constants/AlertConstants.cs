namespace Domain.Constants;

public static class AlertStatus
{
    public const string Triggered = "Triggered";
    public const string Acknowledged = "Acknowledged";
    public const string Resolved = "Resolved";
    public static readonly string[] All = [Triggered, Acknowledged, Resolved];
}

public static class MetricTypes
{
    public const string Cpu = "Cpu";
    public const string Memory = "Memory";
    public const string Disk = "Disk";
    public const string ResponseTime = "ResponseTime";
    public static readonly string[] All = [Cpu, Memory, Disk, ResponseTime];
}

public static class AlertOperators
{
    public const string GreaterThan = "GreaterThan";
    public const string LessThan = "LessThan";
    public static readonly string[] All = [GreaterThan, LessThan];
}