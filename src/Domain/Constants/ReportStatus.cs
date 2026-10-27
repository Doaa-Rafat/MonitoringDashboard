namespace Domain.Constants;

public static class ReportStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public static readonly string[] All = [Pending, Processing, Completed, Failed];
}