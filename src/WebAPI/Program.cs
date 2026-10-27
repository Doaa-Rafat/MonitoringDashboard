using Infrastructure;
using Application;
using Serilog;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Application.Common.Interfaces;
using WebAPI.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Domain.Constants;
using Infrastructure.Persistence;
using Hangfire;
using WebAPI.Extensions;
using WebAPI.Filters;
using WebAPI.Hubs;
using Infrastructure.Services;
using WebAPI.Middleware;
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddApplication();
// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.PostConfigure<ReportStorageOptions>(o =>
    o.StoragePath = Path.GetFullPath(o.StoragePath, builder.Environment.ContentRootPath));
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
          ?? throw new InvalidOperationException("Jwt settings are missing.");
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes. Set it with user-secrets or an environment variable.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
        
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(RoleNames.Admin));

builder.Services.AddHangfireServices(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy("Client", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));          // required by SignalR when it falls back from WebSockets
builder.Services.AddControllers();
builder.Services.AddApiVersioningSetup();
    builder.Services.AddApiDocumentation();
    builder.Services.AddRateLimitingSetup(builder.Configuration);
    builder.Services.AddHealthChecksSetup();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

// Configure the HTTP request pipeline.
app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
    {
        app.MapOpenApi();                                   
        app.UseSwaggerUI(o =>                               
        {
            o.SwaggerEndpoint("/openapi/v1.json", "Monitoring API v1");
            o.RoutePrefix = "swagger";
            o.DocumentTitle = "Monitoring API";
            o.EnablePersistAuthorization();
        });
    }

app.UseHttpsRedirection();
app.UseCors("Client");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "Monitoring Jobs",
    Authorization = [new HangfireDashboardAuthorizationFilter(app.Environment)]
});
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");
app.RegisterRecurringJobs();
app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
