using System.Linq.Expressions;
using Domain.Constants;
using Domain.Entities;

namespace Application.Features.Reports;

public record ReportDto(int Id, int ServerId, string ServerName, DateTime StartTime, DateTime EndTime,
    string Status, DateTime CreatedAt, DateTime? CompletedAt, bool IsDownloadable);

public record ReportFile(Stream Content, string FileName, string ContentType);

public static class ReportMappings
{
    public static readonly Expression<Func<Report, ReportDto>> Projection = r => new ReportDto(
        r.ReportId, r.ServerId, r.Server.Name, r.StartTime, r.EndTime, r.Status,
        r.CreatedAt, r.CompletedAt, r.Status == ReportStatus.Completed && r.FilePath != null);
}