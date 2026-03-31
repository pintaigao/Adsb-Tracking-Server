using ADSB.Tracker.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSB.Tracker.Server.Data;

/// <summary>
/// EF Core view of the MySQL schema owned by ADSB-Tracker-Server.
/// This service stores schedule jobs, execution history, and tail/hex mappings.
/// </summary>
public sealed class AdsbTrackerDbContext(DbContextOptions<AdsbTrackerDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// User-scoped schedule definitions.
    /// </summary>
    public DbSet<WatchSchedule> WatchSchedules => Set<WatchSchedule>();

    /// <summary>
    /// Execution history for schedules.
    /// </summary>
    public DbSet<WatchExecution> WatchExecutions => Set<WatchExecution>();

    /// <summary>
    /// Optional lookup table used to turn tail numbers into hex codes.
    /// </summary>
    public DbSet<TailHexMapping> TailHexMappings => Set<TailHexMapping>();

    /// <summary>
    /// Database-level mapping rules: table names, lengths, indexes, and relationships.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WatchSchedule>(entity =>
        {
            // High-level job definition: "watch this target on this UTC date/time window".
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
            // Concrete run of one schedule, including source/raw paths and final KML output.
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
            // Tail-based schedules often need this mapping because raw ADS-B logs are keyed by hex.
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
