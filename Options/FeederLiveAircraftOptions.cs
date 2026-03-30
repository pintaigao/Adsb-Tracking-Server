namespace ADSB.Tracker.Server.Options;

public sealed class FeederLiveAircraftOptions
{
    public const string SectionName = "FeederLiveAircraft";
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
