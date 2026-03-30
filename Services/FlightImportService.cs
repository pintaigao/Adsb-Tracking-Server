using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ADSB.Tracker.Server.Data.Entities;
using ADSB.Tracker.Server.Options;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Services;

public sealed class FlightImportService(
    HttpClient httpClient,
    IOptions<FlightTrainingServerOptions> options,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ImportScheduledTrackAsync(
        WatchSchedule schedule,
        WatchExecution execution,
        CancellationToken cancellationToken)
    {
        var baseUrl = ResolveValue(
            configuration["FLIGHT_TRAINING_SERVER_BASE_URL"],
            options.Value.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(execution.OutputKmlPath) || !File.Exists(execution.OutputKmlPath))
        {
            return;
        }

        var rawKml = await File.ReadAllTextAsync(execution.OutputKmlPath, cancellationToken);
        var payload = new
        {
            UserId = schedule.UserId,
            ScheduleId = schedule.Id,
            ExecutionId = execution.Id,
            DisplayName = schedule.DisplayName,
            TargetType = schedule.TargetType,
            TargetValue = schedule.TargetValue,
            RawKml = rawKml,
            RawFilename = Path.GetFileName(execution.OutputKmlPath),
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/flight/internal/track-schedule-import");

        var serviceToken = ResolveValue(
            configuration["FLIGHT_TRAINING_SERVER_SERVICE_TOKEN"],
            options.Value.ServiceToken);
        if (!string.IsNullOrWhiteSpace(serviceToken))
        {
            request.Headers.TryAddWithoutValidation("X-Service-Token", serviceToken);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Flight import callback failed ({(int)response.StatusCode}): {raw}");
    }

    private static string ResolveValue(string? preferred, string fallback)
        => string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();
}
