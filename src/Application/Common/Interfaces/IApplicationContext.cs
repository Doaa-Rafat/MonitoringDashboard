using Domain.Entities;
using Microsoft.EntityFrameworkCore;

public interface IApplicationDbContext
{
    DbSet<Server> Servers { get; }
    DbSet<Metric> Metrics { get; }
    DbSet<Disk> Disks { get; }
    DbSet<Alert> Alerts { get; }
    DbSet<AlertRule> AlertRules { get; }
    DbSet<Report> Reports { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}