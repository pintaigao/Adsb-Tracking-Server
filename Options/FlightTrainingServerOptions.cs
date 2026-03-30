namespace ADSB.Tracker.Server.Options;

public sealed class FlightTrainingServerOptions
{
    public const string SectionName = "FlightTrainingServer";
    public string BaseUrl { get; set; } = string.Empty;
    public string ServiceToken { get; set; } = string.Empty;
}
