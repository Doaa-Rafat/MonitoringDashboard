using Application.Common.Interfaces;
using Hangfire;

namespace Infrastructure.BackgroundJobs;

public class HangfireReportJobScheduler(IBackgroundJobClient client) : IReportJobScheduler
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    public void Schedule(int reportId, string? recipientEmail)
    {
        
        var generateId = client.Enqueue<GenerateReportJob>(j => j.RunAsync(reportId, CancellationToken.None));

        
        client.ContinueJobWith<FinalizeReportJob>(generateId,
            j => j.RunAsync(reportId, recipientEmail, CancellationToken.None),
            JobContinuationOptions.OnAnyFinishedState);

        // 3) Delayed: watchdog
        client.Schedule<ExpireStaleReportJob>(j => j.RunAsync(reportId, CancellationToken.None), Timeout);
    }
}