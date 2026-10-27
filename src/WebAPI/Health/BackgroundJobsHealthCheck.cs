using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebAPI.Health;

public class BackgroundJobsHealthCheck(JobStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var servers = storage.GetMonitoringApi().Servers();
            return Task.FromResult(servers.Count > 0
                ? HealthCheckResult.Healthy($"{servers.Count} Hangfire server(s) running")
                : HealthCheckResult.Degraded("No Hangfire server is running"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire storage is not reachable", ex));
        }
    }
}