using Application.Common.Models;
using Application.Features.Jobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace Infrastructure.BackgroundJobs;

public class HangfireJobMonitoringService(JobStorage storage, IBackgroundJobClient client) : IJobMonitoringService
{
    private const string Queue = "default";

    public PagedResult<JobDto> GetJobs(string state, int page, int pageSize)
    {
        var api = storage.GetMonitoringApi();
        var from = (page - 1) * pageSize;
        List<JobDto> items;
        long total;

        switch (state)
        {
            case "Failed":
                items = api.FailedJobs(from, pageSize).Where(kv => kv.Value is not null)
                    .Select(kv => new JobDto(kv.Key, NameOf(kv.Value.Job), state, kv.Value.FailedAt, null,
                        Trim(kv.Value.ExceptionMessage))).ToList();
                total = api.FailedCount();
                break;
            case "Succeeded":
                items = api.SucceededJobs(from, pageSize).Where(kv => kv.Value is not null)
                    .Select(kv => new JobDto(kv.Key, NameOf(kv.Value.Job), state, kv.Value.SucceededAt,
                        kv.Value.TotalDuration, null)).ToList();
                total = api.SucceededListCount();
                break;
            case "Processing":
                items = api.ProcessingJobs(from, pageSize).Where(kv => kv.Value is not null)
                    .Select(kv => new JobDto(kv.Key, NameOf(kv.Value.Job), state, kv.Value.StartedAt, null, null)).ToList();
                total = api.ProcessingCount();
                break;
            case "Scheduled":
                items = api.ScheduledJobs(from, pageSize).Where(kv => kv.Value is not null)
                    .Select(kv => new JobDto(kv.Key, NameOf(kv.Value.Job), state, kv.Value.EnqueueAt, null, null)).ToList();
                total = api.ScheduledCount();
                break;
            case "Enqueued":
                items = api.EnqueuedJobs(Queue, from, pageSize).Where(kv => kv.Value is not null)
                    .Select(kv => new JobDto(kv.Key, NameOf(kv.Value.Job), state, kv.Value.EnqueuedAt, null, null)).ToList();
                total = api.EnqueuedCount(Queue);
                break;
            default: // Deleted
                items = api.DeletedJobs(from, pageSize).Where(kv => kv.Value is not null)
                    .Select(kv => new JobDto(kv.Key, NameOf(kv.Value.Job), state, kv.Value.DeletedAt, null, null)).ToList();
                total = api.DeletedListCount();
                break;
        }

        return new PagedResult<JobDto>(items, page, pageSize, (int)Math.Min(total, int.MaxValue));
    }

    public IReadOnlyList<RecurringJobSummaryDto> GetRecurringJobs()
    {
        using var connection = storage.GetConnection();
        return connection.GetRecurringJobs()
            .Select(r => new RecurringJobSummaryDto(r.Id, r.Cron, r.NextExecution, r.LastExecution,
                r.LastJobState, Trim(r.Error)))
            .OrderBy(r => r.Id)
            .ToList();
    }

    public JobCountsDto GetCounts()
    {
        var s = storage.GetMonitoringApi().GetStatistics();
        return new JobCountsDto(s.Enqueued, s.Scheduled, s.Processing, s.Succeeded, s.Failed,
            s.Deleted, s.Recurring, (int)s.Servers);
    }

    // Only a job that is currently Failed can be re-queued.
    public bool RetryFailedJob(string jobId) =>
        client.ChangeState(jobId, new EnqueuedState(), FailedState.StateName);

    private static string NameOf(Job? job) => job is null ? "(unknown job)" : $"{job.Type.Name}.{job.Method.Name}";

    private static string? Trim(string? s) => s is { Length: > 500 } ? s[..500] : s;
}