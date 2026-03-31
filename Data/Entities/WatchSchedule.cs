namespace ADSB.Tracker.Server.Data.Entities;

/*
 * Durable schedule definition created by a user.
 * It says which aircraft target to watch and which UTC window to search later.
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

    /* Schedule lifecycle state, for example scheduled/running/completed/archived. */
    public string Status { get; set; } = string.Empty;

    /* Cached pointer to the most recent execution so list endpoints can show latest status cheaply. */
    public long? LatestExecutionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /* Full execution history for this schedule. */
    public List<WatchExecution> Executions { get; set; } = [];
}
