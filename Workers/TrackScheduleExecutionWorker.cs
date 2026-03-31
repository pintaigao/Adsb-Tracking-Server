using ADSB.Tracker.Server.Options;
using ADSB.Tracker.Server.Services;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Workers;

/*
 * 这是后台循环，相当于 schedule 系统的“时钟”。
 * 每次轮询时，它都会让 TrackScheduleService 执行那些已经到期的 UTC 时间窗口。
 */
public sealed class TrackScheduleExecutionWorker(
	IServiceProvider serviceProvider,
	IOptions<TrackerStorageOptions> storageOptions,
	ILogger<TrackScheduleExecutionWorker> logger)
	: BackgroundService {
	/*
	 * 每一轮都解析一个新的 scoped TrackScheduleService，
	 * 这样数据库和 HTTP 依赖可以保持正常的 scoped 生命周期。
	 */
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
