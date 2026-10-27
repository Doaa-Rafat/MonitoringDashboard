using Application.Common.Extensions;
using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Reports.Commands;

public record RequestReportCommand(int ServerId, DateTime StartTime, DateTime EndTime) : IRequest<ReportDto>;

public class RequestReportCommandValidator : AbstractValidator<RequestReportCommand>
{
    public RequestReportCommandValidator()
    {
        RuleFor(x => x.ServerId).GreaterThan(0);
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty()
            .Must(e => e.AsUtc() <= DateTime.UtcNow.AddMinutes(5)).WithMessage("EndTime cannot be in the future.");
        RuleFor(x => x).Must(x => x.StartTime.AsUtc() < x.EndTime.AsUtc())
            .WithMessage("StartTime must be earlier than EndTime.");
        RuleFor(x => x).Must(x => x.EndTime.AsUtc() - x.StartTime.AsUtc() <= TimeSpan.FromDays(31))
            .WithMessage("The report range cannot exceed 31 days.");
    }
}

public class RequestReportCommandHandler(
    IApplicationDbContext db, IReportJobScheduler scheduler, ICurrentUserService current,
    ILogger<RequestReportCommandHandler> logger) : IRequestHandler<RequestReportCommand, ReportDto>
{
    public async Task<ReportDto> Handle(RequestReportCommand request, CancellationToken ct)
    {
        if (!await db.Servers.AnyAsync(s => s.ServerId == request.ServerId, ct))
            throw new NotFoundException(nameof(Server), request.ServerId);

        var report = new Report
        {
            ServerId = request.ServerId,
            StartTime = request.StartTime.AsUtc(),
            EndTime = request.EndTime.AsUtc(),
            Status = ReportStatus.Pending
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);               // the row must exist before any job runs

        try
        {
            scheduler.Schedule(report.ReportId, current.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not schedule jobs for report {ReportId}", report.ReportId);
            report.Status = ReportStatus.Failed;
            report.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return await db.Reports.AsNoTracking()
            .Where(r => r.ReportId == report.ReportId)
            .Select(ReportMappings.Projection)
            .FirstAsync(ct);
    }
}