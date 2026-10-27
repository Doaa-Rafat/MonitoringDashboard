using Application.Features.Reports.Commands;
using Hangfire;
using MediatR;

namespace Infrastructure.BackgroundJobs;

public class GenerateReportJob(ISender mediator)
{
    // No automatic retries: a failure is visible in Hangfire and retried deliberately (manual Retry).
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(int reportId, CancellationToken ct) =>
        mediator.Send(new GenerateReportCommand(reportId), ct);
}

public class FinalizeReportJob(ISender mediator)
{
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [15, 60])]
    public Task RunAsync(int reportId, string? recipientEmail, CancellationToken ct) =>
        mediator.Send(new FinalizeReportCommand(reportId, recipientEmail), ct);
}

public class ExpireStaleReportJob(ISender mediator)
{
    public Task RunAsync(int reportId, CancellationToken ct) =>
        mediator.Send(new ExpireStaleReportCommand(reportId), ct);
}