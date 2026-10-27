using Application.Common.Interfaces;
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
        services.AddSingleton<IRealtimeNotifier, NullRealtimeNotifier>();
        services.AddScoped<CollectMetricsJob>();
        return services;
    }
}