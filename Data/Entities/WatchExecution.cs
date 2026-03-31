namespace ADSB.Tracker.Server.Data.Entities;

/*
 * 这是某条 schedule 的一次具体执行尝试。
 * 它记录原始日志从哪里来，以及最后有没有成功导出 KML。
 */
public sealed class WatchExecution
{
    public long Id { get; set; }
    public long ScheduleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int MatchedPointCount { get; set; }

    /* 原始来源路径。在 SSH 模式下，这里记录的是 Ubuntu 上的远程路径。 */
    public string? RemoteRawPath { get; set; }

    /* 过滤和导出逻辑真正使用的本地工作副本路径。 */
    public string? LocalRawPath { get; set; }

    /* 导出成功后最终生成的 KML 文件路径。 */
    public string? OutputKmlPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public WatchSchedule Schedule { get; set; } = null!;
}
