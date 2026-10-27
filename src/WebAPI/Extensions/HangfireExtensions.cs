using Hangfire;
using Hangfire.SqlServer;
using Infrastructure.BackgroundJobs;

namespace WebAPI.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer(o => o.WorkerCount = 4);
        return services;
    }

    public static void RegisterRecurringJobs(this WebApplication app)
    {
        var minutes = Math.Clamp(app.Configuration.GetValue("Monitoring:CollectionIntervalMinutes", 2), 1, 5);
        var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

        recurring.AddOrUpdate<CollectMetricsJob>(
            "collect-metrics",
            job => job.RunAsync(CancellationToken.None),
            $"*/{minutes} * * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}