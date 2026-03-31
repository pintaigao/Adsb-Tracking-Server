namespace ADSB.Tracker.Server.Data.Entities;

/*
 * One concrete attempt to execute a schedule.
 * Records where the raw log came from and whether a KML export was produced.
 */
public sealed class WatchExecution
{
    public long Id { get; set; }
    public long ScheduleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int MatchedPointCount { get; set; }

    /* Original source path. In SSH mode this is the remote Ubuntu path. */
    public string? RemoteRawPath { get; set; }

    /* Local working copy used by filtering/export logic. */
    public string? LocalRawPath { get; set; }

    /* Final KML output path when export succeeds. */
    public string? OutputKmlPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public WatchSchedule Schedule { get; set; } = null!;
}
