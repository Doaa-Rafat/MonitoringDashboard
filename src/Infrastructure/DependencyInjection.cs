using Application.Common.Interfaces;
using Application.Features.Jobs;
using Infrastructure.Auth;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("DefaultConnection")));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddScoped<DbSeeder>();
        services.AddSingleton<IMetricsCollector, SimulatedMetricsCollector>();
        services.AddScoped<CollectMetricsJob>();
        services.Configure<ReportStorageOptions>(config.GetSection(ReportStorageOptions.SectionName));
        services.AddSingleton<IReportFileStore, LocalReportFileStore>();
        services.AddSingleton<IEmailSender, SimulatedEmailSender>();
        services.AddScoped<IReportJobScheduler, HangfireReportJobScheduler>();
        services.AddScoped<IJobMonitoringService, HangfireJobMonitoringService>();
        services.AddScoped<GenerateReportJob>();
        services.AddScoped<FinalizeReportJob>();
        services.AddScoped<ExpireStaleReportJob>();
        return services;
    }
}