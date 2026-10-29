using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.AlertRules;
using Application.Features.Alerts;
using Application.Features.Metrics.Commands;
using Application.Features.Reports;
using Application.Features.Metrics;
using Domain.Constants;
using Domain.Entities;
using FluentValidation;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Monitoring.Tests.UnitTests;

public class MetricsCollectionTests
{
    [Fact]
    public async Task CollectMetrics_CreatesAndAutoResolvesBreachedAlert()
    {
        await using var db = CreateDb();
        var server = new Server { Name = "web-01", Status = ServerStatus.Up };
        db.Servers.Add(server);
        db.AlertRules.Add(new AlertRule
        {
            MetricType = MetricTypes.Cpu,
            Operator = AlertOperators.GreaterThan,
            Threshold = 80,
            IsEnabled = true
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var samples = new Queue<MetricSample>([
            new(95, 70, 60, 120, ServerStatus.Up),
            new(40, 70, 60, 120, ServerStatus.Up)
        ]);
        var notifier = new RecordingNotifier();
        var handler = CreateHandler(db, _ => samples.Dequeue(), notifier);

        Assert.Equal(1, await handler.Handle(new CollectMetricsCommand(), CancellationToken.None));
        var created = await db.Alerts.SingleAsync();
        Assert.Equal(AlertStatus.Triggered, created.Status);
        Assert.Equal("web-01", notifier.Triggered.Single().ServerName);

        Assert.Equal(1, await handler.Handle(new CollectMetricsCommand(), CancellationToken.None));
        var resolved = await db.Alerts.SingleAsync();
        Assert.Equal(AlertStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Single(notifier.Updated);
    }

    [Fact]
    public async Task CollectMetrics_ContinuesWhenOneServerCollectorFails()
    {
        await using var db = CreateDb();
        db.Servers.AddRange(
            new Server { Name = "failing", Status = ServerStatus.Up },
            new Server { Name = "healthy", Status = ServerStatus.Down });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = CreateHandler(db, serverId => serverId == 1
            ? throw new InvalidOperationException("simulated collection failure")
            : new MetricSample(25, 50, 40, 80, ServerStatus.Up), new RecordingNotifier());

        var count = await handler.Handle(new CollectMetricsCommand(), CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal(1, await db.Metrics.CountAsync());
        Assert.Equal(ServerStatus.Up, (await db.Servers.SingleAsync(s => s.Name == "healthy")).Status);
        Assert.Equal(ServerStatus.Up, (await db.Servers.SingleAsync(s => s.Name == "failing")).Status);
    }

    [Fact]
    public async Task CollectMetrics_DoesNotRaiseThresholdAlertForDownServer()
    {
        await using var db = CreateDb();
        db.Servers.Add(new Server { Name = "offline", Status = ServerStatus.Up });
        db.AlertRules.Add(new AlertRule
        {
            MetricType = MetricTypes.Cpu,
            Operator = AlertOperators.LessThan,
            Threshold = 50,
            IsEnabled = true
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var notifier = new RecordingNotifier();
        var handler = CreateHandler(db,
            _ => new MetricSample(0, 0, 0, 0, ServerStatus.Down), notifier);

        await handler.Handle(new CollectMetricsCommand(), CancellationToken.None);

        Assert.Empty(await db.Alerts.ToListAsync());
        Assert.Empty(notifier.Triggered);
        Assert.Equal(ServerStatus.Down, (await db.Servers.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateAlertRule_RejectsResponseTimeThresholdAboveCreationLimit()
    {
        await using var db = CreateDb();
        var rule = new AlertRule
        {
            MetricType = MetricTypes.ResponseTime,
            Operator = AlertOperators.GreaterThan,
            Threshold = 500,
            IsEnabled = true
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateAlertRuleCommandHandler(db);

        var error = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new UpdateAlertRuleCommand(rule.AlertRuleId, AlertOperators.GreaterThan, 600_001, true),
            CancellationToken.None));

        Assert.Contains("0 - 600000", error.Message);
        Assert.Equal(500, (await db.AlertRules.SingleAsync()).Threshold);
    }

    private static CollectMetricsCommandHandler CreateHandler(ApplicationDbContext db,
        Func<int, MetricSample> sampleForServer, RecordingNotifier notifier) =>
        new(db, new DelegateMetricsCollector(sampleForServer), new AlertEvaluator(db), notifier,
            NullLogger<CollectMetricsCommandHandler>.Instance);

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class DelegateMetricsCollector(Func<int, MetricSample> sampleForServer) : IMetricsCollector
    {
        public Task<MetricSample> CollectAsync(int serverId, string serverName, CancellationToken ct) =>
            Task.FromResult(sampleForServer(serverId));
    }

    private sealed class RecordingNotifier : IRealtimeNotifier
    {
        public List<MetricDto> Metrics { get; } = [];
        public List<AlertDto> Triggered { get; } = [];
        public List<AlertDto> Updated { get; } = [];

        public Task MetricCollectedAsync(MetricDto metric, CancellationToken ct = default)
        {
            Metrics.Add(metric);
            return Task.CompletedTask;
        }

        public Task AlertTriggeredAsync(AlertDto alert, CancellationToken ct = default)
        {
            Triggered.Add(alert);
            return Task.CompletedTask;
        }

        public Task AlertUpdatedAsync(AlertDto alert, CancellationToken ct = default)
        {
            Updated.Add(alert);
            return Task.CompletedTask;
        }

        public Task ReportUpdatedAsync(ReportDto report, CancellationToken ct = default) => Task.CompletedTask;
    }
}
