using System.Text.RegularExpressions;
using Application.Common.Models;
using Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Application.Features.Jobs;

public record JobDto(string Id, string Name, string State, DateTime? Timestamp, long? DurationMs, string? Error);
public record RecurringJobSummaryDto(string Id, string Cron, DateTime? NextExecution,
    DateTime? LastExecution, string? LastJobState, string? Error);
public record JobCountsDto(long Enqueued, long Scheduled, long Processing, long Succeeded,
    long Failed, long Deleted, long Recurring, int Servers);

public static class JobStates
{
    public static readonly string[] All = ["Enqueued", "Scheduled", "Processing", "Succeeded", "Failed", "Deleted"];
}

public interface IJobMonitoringService
{
    PagedResult<JobDto> GetJobs(string state, int page, int pageSize);
    IReadOnlyList<RecurringJobSummaryDto> GetRecurringJobs();
    JobCountsDto GetCounts();
    bool RetryFailedJob(string jobId);
}

public record GetJobsQuery(string State = "Failed", int Page = 1, int PageSize = 20) : IRequest<PagedResult<JobDto>>;
public record GetRecurringJobsQuery : IRequest<IReadOnlyList<RecurringJobSummaryDto>>;
public record GetJobCountsQuery : IRequest<JobCountsDto>;
public record RetryJobCommand(string JobId) : IRequest;

public class GetJobsQueryValidator : AbstractValidator<GetJobsQuery>
{
    public GetJobsQueryValidator()
    {
        RuleFor(x => x.State).Must(s => JobStates.All.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"State must be one of: {string.Join(", ", JobStates.All)}.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class RetryJobCommandValidator : AbstractValidator<RetryJobCommand>
{
    public RetryJobCommandValidator() =>
        RuleFor(x => x.JobId).NotEmpty().Matches(@"^\d{1,18}$").WithMessage("JobId must be numeric.");
}

public class GetJobsQueryHandler(IJobMonitoringService jobs) : IRequestHandler<GetJobsQuery, PagedResult<JobDto>>
{
    public Task<PagedResult<JobDto>> Handle(GetJobsQuery request, CancellationToken ct)
    {
        var state = JobStates.All.First(s => s.Equals(request.State, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(jobs.GetJobs(state, request.Page, request.PageSize));
    }
}

public class GetRecurringJobsQueryHandler(IJobMonitoringService jobs)
    : IRequestHandler<GetRecurringJobsQuery, IReadOnlyList<RecurringJobSummaryDto>>
{
    public Task<IReadOnlyList<RecurringJobSummaryDto>> Handle(GetRecurringJobsQuery request, CancellationToken ct) =>
        Task.FromResult(jobs.GetRecurringJobs());
}

public class GetJobCountsQueryHandler(IJobMonitoringService jobs) : IRequestHandler<GetJobCountsQuery, JobCountsDto>
{
    public Task<JobCountsDto> Handle(GetJobCountsQuery request, CancellationToken ct) =>
        Task.FromResult(jobs.GetCounts());
}

public class RetryJobCommandHandler(IJobMonitoringService jobs) : IRequestHandler<RetryJobCommand>
{
    public Task Handle(RetryJobCommand request, CancellationToken ct)
    {
        if (!jobs.RetryFailedJob(request.JobId))
            throw new ConflictException($"Job {request.JobId} was not found or is not in the Failed state.");
        return Task.CompletedTask;
    }
}