using System.Globalization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using WebAPI.Health;
using WebAPI.OpenApi;

namespace WebAPI.Extensions;

public static class ApiExtensions
{
    public static IServiceCollection AddApiVersioningSetup(this IServiceCollection services)
    {
        services.AddApiVersioning(o =>
            {
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.ReportApiVersions = true;                         // adds api-supported-versions header
                o.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(o =>
            {
                o.GroupNameFormat = "'v'VVV";                       // group name: v1, v2...
                o.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Monitoring Dashboard API",
                    Version = "v1",
                    Description = "Real-time system monitoring: servers, metrics, alerts, reports and background jobs. " +
                                  "Real-time events are delivered through the SignalR hub at /hubs/monitoring."
                };
                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static IServiceCollection AddRateLimitingSetup(this IServiceCollection services, IConfiguration config)
    {
        var permit = config.GetValue("RateLimiting:AuthPermitPerMinute", 10);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Brute-force protection for login/register/refresh, partitioned by client IP.
            o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            o.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Rate limit exceeded. Try again later."
                }, ct);
            };
        });

        return services;
    }

    public static IServiceCollection AddHealthChecksSetup(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"])
            .AddCheck<BackgroundJobsHealthCheck>("background-jobs", tags: ["ready"])
            .AddCheck<MetricsFreshnessHealthCheck>("metrics-freshness", tags: ["ready"]);

        return services;
    }

    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness: the process is up (no dependencies checked).
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        // Readiness: dependencies are reachable (503 when Unhealthy; Degraded stays 200).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthResponseWriter.WriteAsync
        });
    }
}