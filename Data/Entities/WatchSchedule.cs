namespace ADSB.Tracker.Server.Data.Entities;

/*
 * 这是用户创建的持久化 schedule 定义。
 * 它描述的是：后面要在什么 UTC 时间窗口里查哪个飞机目标。
 */
public sealed class WatchSchedule
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public DateOnly WatchDateUtc { get; set; }
    public TimeOnly StartZulu { get; set; }
    public TimeOnly EndZulu { get; set; }

    /* schedule 的生命周期状态，例如 scheduled / running / completed / archived。 */
    public string Status { get; set; } = string.Empty;

    /* 缓存最近一次 execution 的指针，方便列表接口便宜地展示最新状态。 */
    public long? LatestExecutionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /* 这条 schedule 的完整执行历史。 */
    public List<WatchExecution> Executions { get; set; } = [];
}
