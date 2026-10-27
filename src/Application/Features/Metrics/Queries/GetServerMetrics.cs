using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Metrics.Queries;

public record GetServerMetricsQuery(
    int ServerId,
    DateTime? From = null,
    DateTime? To = null,
    string SortBy = "timestamp",
    string SortDirection = "desc",
    int Page = 1,
    int PageSize = 60) : IRequest<PagedResult<MetricDto>>;

public class GetServerMetricsQueryValidator : AbstractValidator<GetServerMetricsQuery>
{
    private static readonly string[] SortFields = ["timestamp", "cpu", "memory", "disk", "responsetime"];

    public GetServerMetricsQueryValidator()
    {
        RuleFor(x => x.ServerId).GreaterThan(0);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x).Must(x => x.From!.Value.AsUtc() <= x.To!.Value.AsUtc())
            .When(x => x.From.HasValue && x.To.HasValue).WithMessage("'From' must be earlier than 'To'.");
        RuleFor(x => x.SortBy).Must(s => SortFields.Contains(s.ToLowerInvariant()))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
        RuleFor(x => x.SortDirection).Must(d => d.ToLowerInvariant() is "asc" or "desc")
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}

public class GetServerMetricsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetServerMetricsQuery, PagedResult<MetricDto>>
{
    public async Task<PagedResult<MetricDto>> Handle(GetServerMetricsQuery request, CancellationToken ct)
    {
        if (!await db.Servers.AnyAsync(s => s.ServerId == request.ServerId, ct))
            throw new NotFoundException(nameof(Server), request.ServerId);

        var query = db.Metrics.AsNoTracking().Where(m => m.ServerId == request.ServerId);

        if (request.From is { } from) { var f = from.AsUtc(); query = query.Where(m => m.Timestamp >= f); }
        if (request.To is { } to)     { var t = to.AsUtc();   query = query.Where(m => m.Timestamp <= t); }

        var total = await query.CountAsync(ct);
        var desc = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);

        var items = await Sort(query, request.SortBy, desc)
            .ThenBy(m => m.MetricId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(MetricMappings.ToDto)
            .ToListAsync(ct);

        return new PagedResult<MetricDto>(items, request.Page, request.PageSize, total);
    }

    private static IOrderedQueryable<Metric> Sort(IQueryable<Metric> q, string sortBy, bool desc) =>
        sortBy.ToLowerInvariant() switch
        {
            "cpu"          => desc ? q.OrderByDescending(m => m.CpuUsage)     : q.OrderBy(m => m.CpuUsage),
            "memory"       => desc ? q.OrderByDescending(m => m.MemoryUsage)  : q.OrderBy(m => m.MemoryUsage),
            "disk"         => desc ? q.OrderByDescending(m => m.DiskUsage)    : q.OrderBy(m => m.DiskUsage),
            "responsetime" => desc ? q.OrderByDescending(m => m.ResponseTime) : q.OrderBy(m => m.ResponseTime),
            _              => desc ? q.OrderByDescending(m => m.Timestamp)    : q.OrderBy(m => m.Timestamp)
        };
}