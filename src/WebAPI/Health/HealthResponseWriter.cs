using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebAPI.Health;

public static class HealthResponseWriter
{
    // Status and descriptions only: never exception messages or connection details.
    public static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = (long)e.Value.Duration.TotalMilliseconds
            })
        });
}