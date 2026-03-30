namespace ADSB.Tracker.Server.Constants;

public static class TrackTargetTypes
{
    public const string Tail = "tail";
    public const string Hex = "hex";
    public const string Flight = "flight";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Tail,
        Hex,
        Flight,
    };
}
