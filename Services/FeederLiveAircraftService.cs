using System.Net.Http.Headers;
using System.Text.Json;
using ADSB.Tracker.Server.Dtos.LiveAircraft;
using ADSB.Tracker.Server.Options;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Services;

/*
 * 这是一个很薄的 HTTP client，用来请求 Ubuntu feeder 的 live-aircraft 接口。
 * 这条链路是实时链路，故意和 schedule/raw-file 导出链路分开。
 */
public sealed class FeederLiveAircraftService(
    HttpClient httpClient,
    IOptions<FeederLiveAircraftOptions> options,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /* 从 feeder 服务抓取当前这一刻的 live-aircraft 快照。 */
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
