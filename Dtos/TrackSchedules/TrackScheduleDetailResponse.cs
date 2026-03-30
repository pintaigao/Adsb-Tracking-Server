namespace ADSB.Tracker.Server.Dtos.TrackSchedules;

public sealed class TrackScheduleDetailResponse : TrackScheduleListItemResponse
{
    public List<TrackExecutionResponse> Executions { get; set; } = [];
}
