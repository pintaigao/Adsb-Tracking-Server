using System.Net.Http.Headers;
using System.Text.Json;
using ADSB.Tracker.Server.Dtos.LiveAircraft;
using ADSB.Tracker.Server.Options;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Services;

/// <summary>
/// Thin HTTP client for the Ubuntu feeder's live-aircraft endpoint.
/// This path is real-time and intentionally separate from the schedule/raw-file pipeline.
/// </summary>
public sealed class FeederLiveAircraftService(
    HttpClient httpClient,
    IOptions<FeederLiveAircraftOptions> options,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Fetch one current live-aircraft snapshot from the feeder service.
    /// </summary>
    public async Task<LiveAircraftResponse> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var feederUrl = ResolveValue(configuration["FEEDER_LIVE_AIRCRAFT_URL"], options.Value.Url);
        if (string.IsNullOrWhiteSpace(feederUrl))
        {
            throw new InvalidOperationException("Feeder live-aircraft URL is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, feederUrl);

        var feederToken = ResolveValue(
            configuration["FEEDER_LIVE_AIRCRAFT_TOKEN"],
            options.Value.Token);
        if (!string.IsNullOrWhiteSpace(feederToken))
        {
            request.Headers.TryAddWithoutValidation("X-Feeder-Token", feederToken);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var snapshot = await JsonSerializer.DeserializeAsync<LiveAircraftResponse>(
            stream,
            JsonOptions,
            cancellationToken);

        return snapshot ?? new LiveAircraftResponse();
    }

    private static string ResolveValue(string? preferred, string fallback)
        => string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();
}
