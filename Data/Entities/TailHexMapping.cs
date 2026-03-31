namespace ADSB.Tracker.Server.Data.Entities;

/* 这是一个可选辅助表，用来把 tail number 解析成原始 ADS-B 日志里的 hex code。 */
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
