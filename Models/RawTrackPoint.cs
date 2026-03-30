using System.Text.Json.Serialization;

namespace ADSB.Tracker.Server.Models;

public sealed class RawTrackPoint
{
    [JsonPropertyName("ts")]
    public double Ts { get; set; }

    [JsonPropertyName("hex")]
    public string? Hex { get; set; }

    [JsonPropertyName("flight")]
    public string? Flight { get; set; }

    [JsonPropertyName("squawk")]
    public string? Squawk { get; set; }

    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lon")]
    public double? Lon { get; set; }

    [JsonPropertyName("alt")]
    public double? Alt { get; set; }

    [JsonPropertyName("gs")]
    public double? Gs { get; set; }

    [JsonPropertyName("track")]
    public double? Track { get; set; }

    [JsonPropertyName("seen_pos")]
    public double? SeenPos { get; set; }

    [JsonPropertyName("vert_rate")]
    public double? VertRate { get; set; }
}
