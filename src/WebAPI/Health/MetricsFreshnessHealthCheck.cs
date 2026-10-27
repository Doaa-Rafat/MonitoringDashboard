using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebAPI.Health;

public class MetricsFreshnessHealthCheck(IApplicationDbContext db, IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var interval = Math.Clamp(config.GetValue("Monitoring:CollectionIntervalMinutes", 2), 1, 5);
        var maxAge = TimeSpan.FromMinutes(interval * 3);

        var latest = await db.Metrics.AsNoTracking().MaxAsync(m => (DateTime?)m.Timestamp, ct);
        if (latest is null) return HealthCheckResult.Degraded("No metrics collected yet");

        var age = DateTime.UtcNow - latest.Value;
        return age <= maxAge
            ? HealthCheckResult.Healthy($"Latest metric is {age.TotalSeconds:0}s old")
            : HealthCheckResult.Degraded($"Latest metric is {age.TotalMinutes:0.#} min old");
    }
}