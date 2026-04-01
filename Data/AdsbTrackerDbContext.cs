using ADSB.Tracker.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSB.Tracker.Server.Data;

/*
 * 这是 ADSB-Tracker-Server 自己拥有的 MySQL schema 在 EF Core 里的映射。
 * 这里定义了 schedule、execution 和 tail/hex mapping 这几张表。
 * 有哪些表
 * 每张表对应哪个实体类
 * 字段长度/索引/关联关系是什么
 */
public sealed class AdsbTrackerDbContext(DbContextOptions<AdsbTrackerDbContext> options) : DbContext(options) {
	/* 用户维度的 schedule 定义。 */
	public DbSet<WatchSchedule> WatchSchedules => Set<WatchSchedule>();

	/* schedule 的执行历史。 */
	public DbSet<WatchExecution> WatchExecutions => Set<WatchExecution>();

	/* 可选的 lookup 表，用于把 tail number 转成 hex code。 */
	public DbSet<TailHexMapping> TailHexMappings => Set<TailHexMapping>();

	/*
	 * 这里放数据库层面的映射规则：表名、字段长度、索引、关联关系。
	 */
	protected override void OnModelCreating(ModelBuilder modelBuilder) {
		modelBuilder.Entity<WatchSchedule>(entity => {
			/* 高层 job 定义：在某个 UTC 日期/时间窗口里监控某个目标。 */
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

		modelBuilder.Entity<WatchExecution>(entity => {
			/* 一次具体执行记录，包含原始数据来源路径和最终 KML 输出路径。 */
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

		modelBuilder.Entity<TailHexMapping>(entity => {
			/* tail 类型的 schedule 往往需要这张映射表，因为原始 ADS-B 日志通常按 hex 识别。 */
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