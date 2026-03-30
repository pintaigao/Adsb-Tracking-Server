namespace ADSB.Tracker.Server.Data.Entities;

public sealed class TailHexMapping
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Tail { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
