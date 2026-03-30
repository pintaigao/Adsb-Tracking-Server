namespace ADSB.Tracker.Server.Contracts.TrackSchedules;

public sealed class TrackExecutionResponse
{
    public long Id { get; set; }
    public long ScheduleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int MatchedPointCount { get; set; }
    public string? StartedAtUtc { get; set; }
    public string? FinishedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DownloadUrl { get; set; }
}
