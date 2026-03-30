namespace ADSB.Tracker.Server.Data.Entities;

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
    public string Status { get; set; } = string.Empty;
    public long? LatestExecutionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<WatchExecution> Executions { get; set; } = [];
}
