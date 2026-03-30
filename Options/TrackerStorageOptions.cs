namespace ADSB.Tracker.Server.Options;

public sealed class TrackerStorageOptions
{
    public const string SectionName = "TrackerStorage";
    public string WorkingDirectory { get; set; } = "data/work";
    public string ExportDirectory { get; set; } = "data/exports";
    public int PollIntervalSeconds { get; set; } = 60;
}
