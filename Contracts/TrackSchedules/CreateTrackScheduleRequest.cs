namespace ADSB.Tracker.Server.Contracts.TrackSchedules;

public sealed class CreateTrackScheduleRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public string WatchDateUtc { get; set; } = string.Empty;
    public string StartZulu { get; set; } = string.Empty;
    public string EndZulu { get; set; } = string.Empty;
}
