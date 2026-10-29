using Domain.Constants;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Application.Features.Metrics.Queries;

namespace Monitoring.Tests.UnitTests;

public class NetworkMetricsQueryTests
{
    [Theory]
    [InlineData("networkin")]
    [InlineData("networkout")]
    public void GetServerMetricsValidator_AllowsNetworkSortFields(string sortBy)
    {
        var result = new GetServerMetricsQueryValidator().Validate(
            new GetServerMetricsQuery(1, SortBy: sortBy));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task GetServerMetrics_MapsNetworkValuesAndSortsByInboundThroughput()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var server = new Server { Name = "network-query", Status = ServerStatus.Up };
        db.Servers.Add(server);
        db.Metrics.AddRange(
            new Metric
            {
                Server = server, CpuUsage = 10, MemoryUsage = 20, DiskUsage = 30, ResponseTime = 40,
                NetworkInKbps = 800, NetworkOutKbps = 300, Status = ServerStatus.Up,
                Timestamp = DateTime.UtcNow.AddMinutes(-1)
            },
            new Metric
            {
                Server = server, CpuUsage = 11, MemoryUsage = 21, DiskUsage = 31, ResponseTime = 41,
                NetworkInKbps = 1200, NetworkOutKbps = 500, Status = ServerStatus.Up,
                Timestamp = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var result = await new GetServerMetricsQueryHandler(db).Handle(
            new GetServerMetricsQuery(server.ServerId, SortBy: "networkin", SortDirection: "desc"),
            CancellationToken.None);

        Assert.Equal(1200, result.Items[0].NetworkInKbps);
        Assert.Equal(500, result.Items[0].NetworkOutKbps);
        Assert.Equal(800, result.Items[1].NetworkInKbps);
        Assert.Equal(300, result.Items[1].NetworkOutKbps);
    }
}
