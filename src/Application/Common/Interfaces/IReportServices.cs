namespace Application.Common.Interfaces;

public interface IReportFileStore
{
    Task<string> SaveAsync(int reportId, string content, CancellationToken ct);
    Stream? OpenRead(string fileName);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

public interface IReportJobScheduler
{
    void Schedule(int reportId, string? recipientEmail);
}