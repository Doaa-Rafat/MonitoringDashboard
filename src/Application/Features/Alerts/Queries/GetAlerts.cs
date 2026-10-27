using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Constants;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Alerts.Queries;

public record GetAlertsQuery(
    string? Status = null,
    int? ServerId = null,
    string? MetricType = null,
    DateTime? From = null,
    DateTime? To = null,
    string SortDirection = "desc",
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<AlertDto>>;

public class GetAlertsQueryValidator : AbstractValidator<GetAlertsQuery>
{
    public GetAlertsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).Must(s => AlertStatus.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Status))
            .WithMessage($"Status must be one of: {string.Join(", ", AlertStatus.All)}.");
        RuleFor(x => x.MetricType).Must(t => MetricTypes.All.Contains(t!))
            .When(x => !string.IsNullOrEmpty(x.MetricType))
            .WithMessage($"MetricType must be one of: {string.Join(", ", MetricTypes.All)}.");
        RuleFor(x => x.SortDirection).Must(d => d.ToLowerInvariant() is "asc" or "desc")
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
        RuleFor(x => x).Must(x => x.From!.Value.AsUtc() <= x.To!.Value.AsUtc())
            .When(x => x.From.HasValue && x.To.HasValue).WithMessage("'From' must be earlier than 'To'.");
    }
}

public class GetAlertsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAlertsQuery, PagedResult<AlertDto>>
{
    public async Task<PagedResult<AlertDto>> Handle(GetAlertsQuery request, CancellationToken ct)
    {
        var query = db.Alerts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(request.Status))     query = query.Where(a => a.Status == request.Status);
        if (request.ServerId is { } sid)               query = query.Where(a => a.ServerId == sid);
        if (!string.IsNullOrEmpty(request.MetricType)) query = query.Where(a => a.MetricType == request.MetricType);
        if (request.From is { } from) { var f = from.AsUtc(); query = query.Where(a => a.CreatedAt >= f); }
        if (request.To is { } to)     { var t = to.AsUtc();   query = query.Where(a => a.CreatedAt <= t); }

        var total = await query.CountAsync(ct);

        var desc = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var ordered = desc ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt);

        var items = await ordered
            .ThenBy(a => a.AlertId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(AlertMappings.Projection)
            .ToListAsync(ct);

        return new PagedResult<AlertDto>(items, request.Page, request.PageSize, total);
    }
}