namespace ADSB.Tracker.Server.Dtos.LiveAircraft;

public sealed class LiveAircraftResponse
{
    public string UpdatedAt { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<LiveAircraftItemResponse> Items { get; set; } = [];
}
