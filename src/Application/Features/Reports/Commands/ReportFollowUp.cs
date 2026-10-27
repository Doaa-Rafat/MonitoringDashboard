using Application.Common.Interfaces;
using Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Reports.Commands;

// Continuation: runs after the generation job (whatever its outcome).
public record FinalizeReportCommand(int ReportId, string? RecipientEmail) : IRequest;

// Delayed: watchdog for reports that never finished.
public record ExpireStaleReportCommand(int ReportId) : IRequest;

public class FinalizeReportCommandHandler(
    IApplicationDbContext db, IEmailSender email, IRealtimeNotifier notifier,
    ILogger<FinalizeReportCommandHandler> logger) : IRequestHandler<FinalizeReportCommand>
{
    public async Task Handle(FinalizeReportCommand request, CancellationToken ct)
    {
        var report = await db.Reports.AsNoTracking()
            .Where(r => r.ReportId == request.ReportId)
            .Select(ReportMappings.Projection)
            .FirstOrDefaultAsync(ct);
        if (report is null) return;

        if (!string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            if (report.Status == ReportStatus.Completed)
                await email.SendAsync(request.RecipientEmail,
                    $"Report #{report.Id} is ready",
                    $"Your performance report for {report.ServerName} is ready to download.", ct);
            else if (report.Status == ReportStatus.Failed)
                await email.SendAsync(request.RecipientEmail,
                    $"Report #{report.Id} failed",
                    $"Generating your report for {report.ServerName} failed. You can request it again.", ct);
        }

        if (report.Status is ReportStatus.Completed or ReportStatus.Failed)
            await ReportNotifications.PublishAsync(notifier, logger, report, ct);
    }
}

public class ExpireStaleReportCommandHandler(
    IApplicationDbContext db, IRealtimeNotifier notifier, ILogger<ExpireStaleReportCommandHandler> logger)
    : IRequestHandler<ExpireStaleReportCommand>
{
    public async Task Handle(ExpireStaleReportCommand request, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.ReportId == request.ReportId, ct);
        if (report is null) return;
        if (report.Status is not (ReportStatus.Pending or ReportStatus.Processing)) return;   // finished in time

        report.Status = ReportStatus.Failed;
        report.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Report {ReportId} timed out and was marked Failed", report.ReportId);

        var dto = await db.Reports.AsNoTracking()
            .Where(r => r.ReportId == request.ReportId).Select(ReportMappings.Projection).FirstAsync(ct);
        await ReportNotifications.PublishAsync(notifier, logger, dto, ct);
    }
}

internal static class ReportNotifications
{
    public static async Task PublishAsync(IRealtimeNotifier notifier, ILogger logger, ReportDto dto, CancellationToken ct)
    {
        try { await notifier.ReportUpdatedAsync(dto, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Real-time notification failed for report {ReportId}", dto.Id);
        }
    }
}