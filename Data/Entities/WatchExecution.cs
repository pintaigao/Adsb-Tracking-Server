namespace ADSB.Tracker.Server.Data.Entities;

public sealed class WatchExecution
{
    public long Id { get; set; }
    public long ScheduleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int MatchedPointCount { get; set; }
    public string? RemoteRawPath { get; set; }
    public string? LocalRawPath { get; set; }
    public string? OutputKmlPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public WatchSchedule Schedule { get; set; } = null!;
}
