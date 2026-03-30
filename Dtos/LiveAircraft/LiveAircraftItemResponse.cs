namespace ADSB.Tracker.Server.Dtos.LiveAircraft;

public sealed class LiveAircraftItemResponse
{
    public string Hex { get; set; } = string.Empty;
    public string? Flight { get; set; }
    public string? Squawk { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lon { get; set; }
    public decimal? Alt { get; set; }
    public decimal? Gs { get; set; }
    public decimal? Track { get; set; }
    public decimal? Seen { get; set; }
}
