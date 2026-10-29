using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Application;
using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Infrastructure.Auth;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WebAPI.Controllers;
using WebAPI.Extensions;
using WebAPI.Middleware;
using WebAPI.Services;

namespace Monitoring.Tests.IntegrationTests;

public class MonitoringApiTests
{
    [Fact]
    public async Task ProtectedServerEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        await using var server = await ApiTestServer.CreateAsync();

        var response = await server.Client.GetAsync("/api/v1/servers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterThenListServers_ReturnsAuthorizedPagedResult()
    {
        await using var server = await ApiTestServer.CreateAsync();
        var token = await RegisterAsync(server, "alice", "alice@example.test");
        server.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await server.Client.GetAsync("/api/v1/servers");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, result.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task RegisteringDuplicateUsername_ReturnsConflict()
    {
        await using var server = await ApiTestServer.CreateAsync();
        await RegisterAsync(server, "alice", "alice@example.test");

        var response = await server.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            userName = "alice",
            email = "other@example.test",
            password = "Password123"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CannotCreateServer()
    {
        await using var server = await ApiTestServer.CreateAsync();
        var token = await RegisterAsync(server, "alice", "alice@example.test");
        server.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await server.Client.PostAsJsonAsync("/api/v1/servers", new
        {
            name = "api-test",
            ipAddress = "192.0.2.20",
            description = "Integration test server"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanCreateServerAndQueueReport()
    {
        await using var server = await ApiTestServer.CreateAsync();
        var login = await server.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = "admin",
            password = "AdminPass123"
        });
        Assert.True(login.IsSuccessStatusCode, $"{await login.Content.ReadAsStringAsync()}\n{server.Logs}");
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        server.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginBody.GetProperty("accessToken").GetString());

        var create = await server.Client.PostAsJsonAsync("/api/v1/servers", new
        {
            name = "api-test",
            ipAddress = "192.0.2.20",
            description = "Integration test server"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var serverBody = await create.Content.ReadFromJsonAsync<JsonElement>();
        var serverId = serverBody.GetProperty("id").GetInt32();

        var now = DateTime.UtcNow;
        var report = await server.Client.PostAsJsonAsync("/api/v1/reports", new
        {
            serverId,
            startTime = now.AddHours(-1),
            endTime = now
        });

        Assert.Equal(HttpStatusCode.Accepted, report.StatusCode);
        var reportBody = await report.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", reportBody.GetProperty("status").GetString());
        Assert.Equal(serverId, reportBody.GetProperty("serverId").GetInt32());
    }

    private static async Task<string> RegisterAsync(ApiTestServer server, string userName, string email)
    {
        var response = await server.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            userName,
            email,
            password = "Password123"
        });
        Assert.True(response.IsSuccessStatusCode, $"{await response.Content.ReadAsStringAsync()}\n{server.Logs}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }
}

internal sealed class ApiTestServer : IAsyncDisposable
{
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes-long";
    private readonly WebApplication app;
    private readonly CapturingLoggerProvider loggerProvider;

    public HttpClient Client { get; }
    public string Logs => loggerProvider.ToString();

    private ApiTestServer(WebApplication app, CapturingLoggerProvider loggerProvider)
    {
        this.app = app;
        this.loggerProvider = loggerProvider;
        Client = app.GetTestClient();
    }

    public static async Task<ApiTestServer> CreateAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(AuthController).Assembly.GetName().Name
        });
        builder.WebHost.UseTestServer();
        var loggerProvider = new CapturingLoggerProvider();
        builder.Logging.AddProvider(loggerProvider);
        var databaseName = $"MonitoringApiTests-{Guid.NewGuid():N}";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "MonitoringApiTests",
            ["Jwt:Audience"] = "MonitoringApiTests.Client",
            ["Jwt:Key"] = SigningKey,
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7"
        });

        builder.Services.AddApplication();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IApplicationDbContext>(services =>
            services.GetRequiredService<ApplicationDbContext>());
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
        builder.Services.AddSingleton<IPasswordHashService, PasswordHashService>();
        builder.Services.AddScoped<IReportJobScheduler, NoopReportJobScheduler>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApiVersioningSetup();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "MonitoringApiTests",
                ValidateAudience = true,
                ValidAudience = "MonitoringApiTests.Client",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "unique_name",
                RoleClaimType = "role"
            };
        });
        builder.Services.AddAuthorizationBuilder().AddPolicy("AdminOnly", policy =>
            policy.RequireRole(RoleNames.Admin));

        var app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var adminRole = new Role { Name = RoleNames.Admin };
            var userRole = new Role { Name = RoleNames.User };
            db.Roles.AddRange(adminRole, userRole);
            db.Users.Add(new User
            {
                UserName = "admin",
                Email = "admin@example.test",
                PasswordHash = new PasswordHashService().Hash("AdminPass123"),
                Roles = { adminRole }
            });
            await db.SaveChangesAsync();
        }

        await app.StartAsync();
        return new ApiTestServer(app, loggerProvider);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> messages = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
    public void Dispose() { }
    public override string ToString() => string.Join(Environment.NewLine, messages);

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) messages.Enqueue(exception.ToString());
        }
    }
}

internal sealed class NoopReportJobScheduler : IReportJobScheduler
{
    public void Schedule(int reportId, string? recipientEmail) { }
}
