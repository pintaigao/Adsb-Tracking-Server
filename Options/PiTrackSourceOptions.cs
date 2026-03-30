namespace ADSB.Tracker.Server.Options;

public sealed class PiTrackSourceOptions
{
    public const string SectionName = "PiTrackSource";
    public string RawRootPath { get; set; } = string.Empty;
}
