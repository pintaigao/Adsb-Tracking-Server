namespace ADSB.Tracker.Server.Contracts.TrackSchedules;

public sealed class TrackScheduleDetailResponse : TrackScheduleListItemResponse
{
    public List<TrackExecutionResponse> Executions { get; set; } = [];
}
