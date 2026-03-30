namespace ADSB.Tracker.Server.Dtos.TrackSchedules;

public class TrackScheduleListItemResponse
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public string WatchDateUtc { get; set; } = string.Empty;
    public string StartZulu { get; set; } = string.Empty;
    public string EndZulu { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedAtUtc { get; set; } = string.Empty;
    public TrackExecutionResponse? LatestExecution { get; set; }
}
