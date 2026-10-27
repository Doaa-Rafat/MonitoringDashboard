using System;
using System.Collections.Generic;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Alert> Alerts { get; set; }

    public virtual DbSet<AlertRule> AlertRules { get; set; }

    public virtual DbSet<Disk> Disks { get; set; }

    public virtual DbSet<Metric> Metrics { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Server> Servers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasIndex(e => e.AlertRuleId, "IX_Alerts_AlertRuleId");

            entity.HasIndex(e => new { e.ServerId, e.CreatedAt }, "IX_Alerts_ServerId_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_Alerts_Status_CreatedAt").IsDescending(false, true);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Alerts_CreatedAt");
            entity.Property(e => e.MetricType).HasMaxLength(50);
            entity.Property(e => e.ResolvedAt).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Triggered", "DF_Alerts_Status");

            entity.HasOne(d => d.AlertRule).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.AlertRuleId)
                .HasConstraintName("FK_Alerts_AlertRules");

            entity.HasOne(d => d.Server).WithMany(p => p.Alerts)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alerts_Servers");
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasIndex(e => e.ServerId, "IX_AlertRules_ServerId");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_AlertRules_CreatedAt");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_AlertRules_IsEnabled");
            entity.Property(e => e.MetricType).HasMaxLength(50);
            entity.Property(e => e.Operator)
                .HasMaxLength(20)
                .HasDefaultValue("GreaterThan", "DF_AlertRules_Operator");

            entity.HasOne(d => d.Server).WithMany(p => p.AlertRules)
                .HasForeignKey(d => d.ServerId)
                .HasConstraintName("FK_AlertRules_Servers");
        });

        modelBuilder.Entity<Disk>(entity =>
        {
            entity.HasIndex(e => new { e.ServerId, e.Timestamp }, "IX_Disks_ServerId_Timestamp").IsDescending(false, true);

            entity.Property(e => e.DriveLetter).HasMaxLength(5);
            entity.Property(e => e.FreeSpaceMb).HasColumnName("FreeSpaceMB");
            entity.Property(e => e.Timestamp)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Disks_Timestamp");
            entity.Property(e => e.TotalSpaceMb).HasColumnName("TotalSpaceMB");

            entity.HasOne(d => d.Server).WithMany(p => p.Disks)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Disks_Servers");
        });

        modelBuilder.Entity<Metric>(entity =>
        {
            entity.HasIndex(e => new { e.ServerId, e.Timestamp }, "IX_Metrics_ServerId_Timestamp").IsDescending(false, true);

            entity.HasIndex(e => e.Timestamp, "IX_Metrics_Timestamp");

            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.Timestamp)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Metrics_Timestamp");

            entity.HasOne(d => d.Server).WithMany(p => p.Metrics)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Metrics_Servers");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasIndex(e => new { e.ServerId, e.CreatedAt }, "IX_Reports_ServerId_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => e.Status, "IX_Reports_Status");

            entity.Property(e => e.CompletedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Reports_CreatedAt");
            entity.Property(e => e.EndTime).HasPrecision(3);
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.StartTime).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending", "DF_Reports_Status");

            entity.HasOne(d => d.Server).WithMany(p => p.Reports)
                .HasForeignKey(d => d.ServerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Servers");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Roles_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Server>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Servers_Name").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Servers_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .HasColumnName("IPAddress");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Up", "DF_Servers_Status");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.HasIndex(e => e.UserName, "UQ_Users_UserName").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Users_CreatedAt");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.RefreshToken).HasMaxLength(500);
            entity.Property(e => e.RefreshTokenExpiry).HasPrecision(3);
            entity.Property(e => e.UserName).HasMaxLength(50);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserRoles_Roles"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserRoles_Users"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("UserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_UserRoles_RoleId");
                    });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
