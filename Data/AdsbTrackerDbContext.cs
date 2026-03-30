using ADSB.Tracker.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSB.Tracker.Server.Data;

public sealed class AdsbTrackerDbContext(DbContextOptions<AdsbTrackerDbContext> options)
    : DbContext(options)
{
    public DbSet<WatchSchedule> WatchSchedules => Set<WatchSchedule>();
    public DbSet<WatchExecution> WatchExecutions => Set<WatchExecution>();
    public DbSet<TailHexMapping> TailHexMappings => Set<TailHexMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WatchSchedule>(entity =>
        {
            entity.ToTable("watch_schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TargetType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TargetValue).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => new { x.Status, x.WatchDateUtc });
            entity.HasMany(x => x.Executions)
                .WithOne(x => x.Schedule)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WatchExecution>(entity =>
        {
            entity.ToTable("watch_executions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RemoteRawPath).HasMaxLength(500);
            entity.Property(x => x.LocalRawPath).HasMaxLength(500);
            entity.Property(x => x.OutputKmlPath).HasMaxLength(500);
            entity.HasIndex(x => new { x.ScheduleId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<TailHexMapping>(entity =>
        {
            entity.ToTable("tail_hex_mappings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(64);
            entity.Property(x => x.Tail).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Hex).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Hex);
            entity.HasIndex(x => new { x.UserId, x.Tail }).IsUnique();
        });
    }
}
