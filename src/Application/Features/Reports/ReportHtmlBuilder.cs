using System.Globalization;
using System.Net;
using System.Text;

namespace Application.Features.Reports;

public record MetricSummary(double Average, double Min, double Max);
public record ReportAlertRow(string MetricType, double Value, double Threshold, string Status, DateTime CreatedAt);

public record ReportData(
    string ServerName, DateTime Start, DateTime End, DateTime GeneratedAt,
    int Samples, int UpSamples,
    MetricSummary? Cpu, MetricSummary? Memory, MetricSummary? Disk, MetricSummary? ResponseTime,
    int AlertCount, IReadOnlyList<ReportAlertRow> Alerts)
{
    public double UptimePercent => Samples == 0 ? 0 : Math.Round(100.0 * UpSamples / Samples, 2);
}

public static class ReportHtmlBuilder
{
    public static string Build(ReportData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Performance report - {E(d.ServerName)}</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:2rem auto;max-width:860px;color:#222}" +
                      "table{border-collapse:collapse;width:100%;margin:1rem 0}th,td{border:1px solid #ddd;padding:.45rem .7rem;text-align:left}" +
                      "th{background:#f3f4f6}.muted{color:#666}</style></head><body>");
        sb.AppendLine($"<h1>Performance report: {E(d.ServerName)}</h1>");
        sb.AppendLine($"<p class=\"muted\">Period: {T(d.Start)} to {T(d.End)}<br>Generated: {T(d.GeneratedAt)}</p>");

        if (d.Samples == 0)
        {
            sb.AppendLine("<p>No metrics were collected for this server in the selected period.</p>");
        }
        else
        {
            sb.AppendLine($"<p>Samples: <b>{d.Samples}</b> | Uptime: <b>{N(d.UptimePercent)}%</b> ({d.UpSamples} of {d.Samples} samples were Up)</p>");
            sb.AppendLine("<h2>Metrics (averages exclude samples where the server was down)</h2>");
            sb.AppendLine("<table><tr><th>Metric</th><th>Average</th><th>Min</th><th>Max</th></tr>");
            Row(sb, "CPU (%)", d.Cpu);
            Row(sb, "Memory (%)", d.Memory);
            Row(sb, "Disk (%)", d.Disk);
            Row(sb, "Response time (ms)", d.ResponseTime);
            sb.AppendLine("</table>");
        }

        sb.AppendLine($"<h2>Alerts ({d.AlertCount})</h2>");
        if (d.Alerts.Count == 0)
        {
            sb.AppendLine("<p>No alerts in this period.</p>");
        }
        else
        {
            sb.AppendLine("<table><tr><th>Time</th><th>Metric</th><th>Value</th><th>Threshold</th><th>Status</th></tr>");
            foreach (var a in d.Alerts)
                sb.AppendLine($"<tr><td>{T(a.CreatedAt)}</td><td>{E(a.MetricType)}</td><td>{N(a.Value)}</td><td>{N(a.Threshold)}</td><td>{E(a.Status)}</td></tr>");
            sb.AppendLine("</table>");
            if (d.AlertCount > d.Alerts.Count)
                sb.AppendLine($"<p class=\"muted\">Showing the latest {d.Alerts.Count} of {d.AlertCount} alerts.</p>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void Row(StringBuilder sb, string label, MetricSummary? m)
    {
        if (m is null) return;
        sb.AppendLine($"<tr><td>{E(label)}</td><td>{N(m.Average)}</td><td>{N(m.Min)}</td><td>{N(m.Max)}</td></tr>");
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);   // everything user-controlled is encoded
    private static string N(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    private static string T(DateTime t) => t.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}