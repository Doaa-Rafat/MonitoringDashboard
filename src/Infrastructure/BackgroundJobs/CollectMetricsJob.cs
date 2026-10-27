using Application.Features.Metrics.Commands;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class CollectMetricsJob(ISender mediator, ILogger<CollectMetricsJob> logger)
{
    // No overlapping runs, and a couple of retries for transient failures (e.g. a DB blip).
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [10, 30])]
    public async Task RunAsync(CancellationToken ct)
    {
        var count = await mediator.Send(new CollectMetricsCommand(), ct);
        logger.LogInformation("Collected metrics for {Count} servers", count);
    }
}