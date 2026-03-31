using ADSB.Tracker.Server.Options;
using ADSB.Tracker.Server.Services;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Workers;

/// <summary>
/// Background loop that acts as the schedule "clock".
/// Every poll interval it asks TrackScheduleService to execute UTC windows that are now due.
/// </summary>
public sealed class TrackScheduleExecutionWorker(
	IServiceProvider serviceProvider,
	IOptions<TrackerStorageOptions> storageOptions,
	ILogger<TrackScheduleExecutionWorker> logger)
	: BackgroundService {
	/// <summary>
	/// Resolve a fresh scoped TrackScheduleService each iteration so database and HTTP dependencies
	/// follow normal scoped lifetimes.
	/// </summary>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		var intervalSeconds = Math.Max(storageOptions.Value.PollIntervalSeconds, 15);

		while (!stoppingToken.IsCancellationRequested) {
			try {
				using var scope = serviceProvider.CreateScope();
				var service = scope.ServiceProvider.GetRequiredService<TrackScheduleService>();
				await service.ExecuteDueSchedulesAsync(stoppingToken);
			} catch (Exception ex) {
				logger.LogError(ex, "Track schedule execution worker iteration failed");
			}

			await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
		}
	}
}
