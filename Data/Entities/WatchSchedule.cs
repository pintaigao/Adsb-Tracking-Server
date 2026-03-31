namespace ADSB.Tracker.Server.Data.Entities;

/// <summary>
/// Durable schedule definition created by a user.
/// It says which aircraft target to watch and which UTC window to search later.
/// </summary>
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

    /// <summary>
    /// Schedule lifecycle state, for example scheduled/running/completed/archived.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Cached pointer to the most recent execution so list endpoints can show latest status cheaply.
    /// </summary>
    public long? LatestExecutionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Full execution history for this schedule.
    /// </summary>
    public List<WatchExecution> Executions { get; set; } = [];
}
