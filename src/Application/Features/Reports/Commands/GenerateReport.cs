using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Reports.Commands;

public record GenerateReportCommand(int ReportId) : IRequest;

public class GenerateReportCommandHandler(
    IApplicationDbContext db, IReportFileStore store, ILogger<GenerateReportCommandHandler> logger)
    : IRequestHandler<GenerateReportCommand>
{
    public async Task Handle(GenerateReportCommand request, CancellationToken ct)
    {
        var report = await db.Reports.Include(r => r.Server)
                         .FirstOrDefaultAsync(r => r.ReportId == request.ReportId, ct)
                     ?? throw new NotFoundException(nameof(Report), request.ReportId);

        if (report.Status == ReportStatus.Completed) return;     // idempotent: a retried job is harmless

        report.Status = ReportStatus.Processing;
        report.CompletedAt = null;
        await db.SaveChangesAsync(ct);

        try
        {
            var data = await BuildDataAsync(report, ct);
            var html = ReportHtmlBuilder.Build(data);
            report.FilePath = await store.SaveAsync(report.ReportId, html, ct);
            report.Status = ReportStatus.Completed;
            report.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Report {ReportId} failed", report.ReportId);
            report.Status = ReportStatus.Failed;
            report.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;   // the Hangfire job must fail too, so it shows up and can be retried
        }
    }

    private async Task<ReportData> BuildDataAsync(Report report, CancellationToken ct)
    {
        var start = report.StartTime;
        var end = report.EndTime;

        var range = db.Metrics.AsNoTracking()
            .Where(m => m.ServerId == report.ServerId && m.Timestamp >= start && m.Timestamp <= end);

        var samples = await range.CountAsync(ct);
        var up = range.Where(m => m.Status == ServerStatus.Up);
        var upSamples = await up.CountAsync(ct);

        var agg = await up.GroupBy(_ => 1).Select(g => new
        {
            AvgCpu = g.Average(m => m.CpuUsage),      MinCpu = g.Min(m => m.CpuUsage),      MaxCpu = g.Max(m => m.CpuUsage),
            AvgMem = g.Average(m => m.MemoryUsage),   MinMem = g.Min(m => m.MemoryUsage),   MaxMem = g.Max(m => m.MemoryUsage),
            AvgDisk = g.Average(m => m.DiskUsage),    MinDisk = g.Min(m => m.DiskUsage),    MaxDisk = g.Max(m => m.DiskUsage),
            AvgResp = g.Average(m => m.ResponseTime), MinResp = g.Min(m => m.ResponseTime), MaxResp = g.Max(m => m.ResponseTime)
        }).FirstOrDefaultAsync(ct);

        var alertsInRange = db.Alerts.AsNoTracking()
            .Where(a => a.ServerId == report.ServerId && a.CreatedAt >= start && a.CreatedAt <= end);
        var alertCount = await alertsInRange.CountAsync(ct);
        var alerts = await alertsInRange.OrderByDescending(a => a.CreatedAt).Take(50)
            .Select(a => new ReportAlertRow(a.MetricType, a.MetricValue, a.Threshold, a.Status, a.CreatedAt))
            .ToListAsync(ct);

        return new ReportData(
            report.Server.Name, start, end, DateTime.UtcNow, samples, upSamples,
            agg is null ? null : new MetricSummary(agg.AvgCpu, agg.MinCpu, agg.MaxCpu),
            agg is null ? null : new MetricSummary(agg.AvgMem, agg.MinMem, agg.MaxMem),
            agg is null ? null : new MetricSummary(agg.AvgDisk, agg.MinDisk, agg.MaxDisk),
            agg is null ? null : new MetricSummary(agg.AvgResp, agg.MinResp, agg.MaxResp),
            alertCount, alerts);
    }
}