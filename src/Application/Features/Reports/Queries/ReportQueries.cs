using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reports.Queries;

public record GetReportsQuery(int? ServerId = null, string? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ReportDto>>;
public record GetReportByIdQuery(int Id) : IRequest<ReportDto>;
public record GetReportFileQuery(int Id) : IRequest<ReportFile>;

public class GetReportsQueryValidator : AbstractValidator<GetReportsQuery>
{
    public GetReportsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).Must(s => ReportStatus.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Status))
            .WithMessage($"Status must be one of: {string.Join(", ", ReportStatus.All)}.");
    }
}

public class GetReportsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetReportsQuery, PagedResult<ReportDto>>
{
    public async Task<PagedResult<ReportDto>> Handle(GetReportsQuery request, CancellationToken ct)
    {
        var query = db.Reports.AsNoTracking().AsQueryable();
        if (request.ServerId is { } sid) query = query.Where(r => r.ServerId == sid);
        if (!string.IsNullOrEmpty(request.Status)) query = query.Where(r => r.Status == request.Status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReportId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(ReportMappings.Projection)
            .ToListAsync(ct);

        return new PagedResult<ReportDto>(items, request.Page, request.PageSize, total);
    }
}

public class GetReportByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetReportByIdQuery, ReportDto>
{
    public async Task<ReportDto> Handle(GetReportByIdQuery request, CancellationToken ct) =>
        await db.Reports.AsNoTracking().Where(r => r.ReportId == request.Id)
            .Select(ReportMappings.Projection).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Report), request.Id);
}

public class GetReportFileQueryHandler(IApplicationDbContext db, IReportFileStore store)
    : IRequestHandler<GetReportFileQuery, ReportFile>
{
    public async Task<ReportFile> Handle(GetReportFileQuery request, CancellationToken ct)
    {
        var report = await db.Reports.AsNoTracking()
            .Where(r => r.ReportId == request.Id)
            .Select(r => new { r.ReportId, r.Status, r.FilePath })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Report), request.Id);

        if (report.Status != ReportStatus.Completed || report.FilePath is null)
            throw new ConflictException($"The report is not ready (status: {report.Status}).");

        var stream = store.OpenRead(report.FilePath)
                     ?? throw new NotFoundException("Report file", request.Id);

        return new ReportFile(stream, $"report-{report.ReportId}.html", "text/html; charset=utf-8");
    }
}