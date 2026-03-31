using System.Globalization;
using ADSB.Tracker.Server.Constants;
using ADSB.Tracker.Server.Dtos.TrackSchedules;
using ADSB.Tracker.Server.Data;
using ADSB.Tracker.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADSB.Tracker.Server.Services;

/*
 * 这是 ADSB-Tracker-Server 里 schedule 导出链路的主应用服务。
 * Controller 用它做 schedule 相关接口，后台 worker 也用它执行到期任务。
 */
public sealed class TrackScheduleService(AdsbTrackerDbContext dbContext, PiTrackSourceService piTrackSourceService, TrackExportService trackExportService, FlightImportService flightImportService, ILogger<TrackScheduleService> logger) {
	/* 创建一条新的 schedule 定义。这个阶段还不会去读 raw 数据。 */
	public async Task<TrackScheduleDetailResponse> CreateAsync(
		string userId,
		CreateTrackScheduleRequest request,
		CancellationToken cancellationToken) {
		var normalized = NormalizeAndValidate(request);
		var now = DateTime.UtcNow;

		var schedule = new WatchSchedule {
			UserId = userId,
			DisplayName = normalized.DisplayName,
			TargetType = normalized.TargetType,
			TargetValue = normalized.TargetValue,
			WatchDateUtc = normalized.WatchDateUtc,
			StartZulu = normalized.StartZulu,
			EndZulu = normalized.EndZulu,
			Status = TrackScheduleStatuses.Scheduled,
			CreatedAtUtc = now,
			UpdatedAtUtc = now,
		};

		dbContext.WatchSchedules.Add(schedule);
		await dbContext.SaveChangesAsync(cancellationToken);

		return MapDetail(schedule, null, []);
	}

	/*
	 * 列出当前活跃的 schedule，并补上每条 schedule 的最近一次 execution 摘要。
	 * 已归档的 schedule 会从默认列表里排除。
	 */
	public async Task<IReadOnlyList<TrackScheduleListItemResponse>> ListAsync(string userId, CancellationToken cancellationToken) {
		var schedules = await dbContext.WatchSchedules
			.AsNoTracking()
			.Where(x => x.UserId == userId && x.Status != TrackScheduleStatuses.Archived)
			.OrderByDescending(x => x.CreatedAtUtc)
			.ToListAsync(cancellationToken);

		var latestExecutions = await LoadLatestExecutionsAsync(schedules, cancellationToken);

		return schedules
			.Select(schedule => {
				latestExecutions.TryGetValue(schedule.Id, out var execution);
				return MapListItem(schedule, execution);
			})
			.ToList();
	}

	/* 加载单条 schedule 以及它的执行历史。 */
	public async Task<TrackScheduleDetailResponse?> GetAsync(
		string userId,
		long id,
		CancellationToken cancellationToken) {
		var schedule = await dbContext.WatchSchedules
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);

		if (schedule is null) {
			return null;
		}

		var executions = await dbContext.WatchExecutions
			.AsNoTracking()
			.Where(x => x.UserId == userId && x.ScheduleId == id)
			.OrderByDescending(x => x.CreatedAtUtc)
			.ToListAsync(cancellationToken);

		return MapDetail(schedule, executions.FirstOrDefault(), executions);
	}

	/* 只有 schedule 还没开始跑的时候，才允许取消。 */
	public async Task<bool> CancelAsync(string userId, long id, CancellationToken cancellationToken) {
		var schedule = await dbContext.WatchSchedules
			.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);

		if (schedule is null || !string.Equals(schedule.Status, TrackScheduleStatuses.Scheduled, StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		schedule.Status = TrackScheduleStatuses.Cancelled;
		schedule.UpdatedAtUtc = DateTime.UtcNow;
		await dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	/* 已结束 schedule 的软删除。记录仍保留在 MySQL，只是不出现在默认列表里。 */
	public async Task<bool> ArchiveAsync(string userId, long id, CancellationToken cancellationToken) {
		var schedule = await dbContext.WatchSchedules
			.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id, cancellationToken);

		if (schedule is null || !CanArchive(schedule.Status)) {
			return false;
		}

		schedule.Status = TrackScheduleStatuses.Archived;
		schedule.UpdatedAtUtc = DateTime.UtcNow;
		await dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	/* 返回某条 schedule 的所有 execution 记录。 */
	public async Task<IReadOnlyList<TrackExecutionResponse>?> ListExecutionsAsync(
		string userId,
		long scheduleId,
		CancellationToken cancellationToken) {
		var exists = await dbContext.WatchSchedules
			.AsNoTracking()
			.AnyAsync(x => x.UserId == userId && x.Id == scheduleId, cancellationToken);

		if (!exists) {
			return null;
		}

		var executions = await dbContext.WatchExecutions
			.AsNoTracking()
			.Where(x => x.UserId == userId && x.ScheduleId == scheduleId)
			.OrderByDescending(x => x.CreatedAtUtc)
			.ToListAsync(cancellationToken);

		return executions.Select(MapExecution).ToList();
	}

	/* 如果某次 execution 成功导出文件，返回它对应的下载路径。 */
	public async Task<(string FilePath, string DownloadName)?> GetExecutionDownloadAsync(
		string userId,
		long executionId,
		CancellationToken cancellationToken) {
		var execution = await dbContext.WatchExecutions
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == executionId, cancellationToken);

		if (execution is null || string.IsNullOrWhiteSpace(execution.OutputKmlPath) || !File.Exists(execution.OutputKmlPath)) {
			return null;
		}

		return (execution.OutputKmlPath, Path.GetFileName(execution.OutputKmlPath));
	}

	/* Worker 从这里进入：找出 UTC 结束时间已到的 schedule 并立即执行。 */
	public async Task ExecuteDueSchedulesAsync(CancellationToken cancellationToken) {
		var now = DateTime.UtcNow;
		var nowDate = DateOnly.FromDateTime(now);
		var nowTime = TimeOnly.FromDateTime(now);

		var dueSchedules = await dbContext.WatchSchedules
			.Where(x => x.Status == TrackScheduleStatuses.Scheduled && (x.WatchDateUtc < nowDate || (x.WatchDateUtc == nowDate && x.EndZulu <= nowTime)))
			.OrderBy(x => x.WatchDateUtc)
			.ThenBy(x => x.EndZulu)
			.ToListAsync(cancellationToken);

		foreach (var schedule in dueSchedules) {
			await ExecuteSingleScheduleAsync(schedule, cancellationToken);
		}
	}

	/*
	 * 这是完整的 schedule 执行流水线：
	 * 1. 取 raw 数据
	 * 2. 过滤并导出 KML
	 * 3. 落 execution 结果
	 * 4. 回调 Flight-Training
	 */
	private async Task ExecuteSingleScheduleAsync(WatchSchedule schedule, CancellationToken cancellationToken) {
		var now = DateTime.UtcNow;
		schedule.Status = TrackScheduleStatuses.Running;
		schedule.UpdatedAtUtc = now;

		var execution = new WatchExecution {
			ScheduleId = schedule.Id,
			UserId = schedule.UserId,
			Status = TrackScheduleStatuses.Running,
			StartedAtUtc = now,
			CreatedAtUtc = now,
		};

		dbContext.WatchExecutions.Add(execution);
		await dbContext.SaveChangesAsync(cancellationToken);

		try {
			var resolvedTargetValue = await ResolveTargetValueAsync(schedule, cancellationToken);
			var fetched = await piTrackSourceService.FetchRawFileAsync(schedule.WatchDateUtc, execution.Id, cancellationToken);

			if (fetched is null) {
				execution.Status = TrackScheduleStatuses.NoData;
				execution.ErrorMessage = "Raw ADS-B log not found for the requested UTC date.";
				execution.FinishedAtUtc = DateTime.UtcNow;
				schedule.Status = TrackScheduleStatuses.NoData;
				schedule.LatestExecutionId = execution.Id;
				schedule.UpdatedAtUtc = DateTime.UtcNow;
				await dbContext.SaveChangesAsync(cancellationToken);
				return;
			}

			execution.RemoteRawPath = fetched.Value.RemotePath;
			execution.LocalRawPath = fetched.Value.LocalPath;
			var export = await trackExportService.ExportAsync(fetched.Value.LocalPath, schedule.TargetType, resolvedTargetValue, schedule.DisplayName, schedule.WatchDateUtc, schedule.StartZulu, schedule.EndZulu, execution.Id, cancellationToken);

			execution.MatchedPointCount = export.MatchedPointCount;
			execution.OutputKmlPath = export.OutputPath;
			execution.FinishedAtUtc = DateTime.UtcNow;
			execution.Status = export.OutputPath is null ? TrackScheduleStatuses.NoData : TrackScheduleStatuses.Completed;

			schedule.Status = execution.Status;
			schedule.LatestExecutionId = execution.Id;
			schedule.UpdatedAtUtc = DateTime.UtcNow;

			await dbContext.SaveChangesAsync(cancellationToken);

			if (!string.IsNullOrWhiteSpace(execution.OutputKmlPath)) {
				try {
					await flightImportService.ImportScheduledTrackAsync(schedule, execution, cancellationToken);
				} catch (Exception importEx) {
					logger.LogError(importEx, "Track schedule execution {ExecutionId} completed but flight import failed", execution.Id);
				}
			}
		} catch (Exception ex) {
			logger.LogError(ex, "Track schedule execution {ExecutionId} failed", execution.Id);
			execution.Status = TrackScheduleStatuses.Failed;
			execution.ErrorMessage = ex.Message;
			execution.FinishedAtUtc = DateTime.UtcNow;

			schedule.Status = TrackScheduleStatuses.Failed;
			schedule.LatestExecutionId = execution.Id;
			schedule.UpdatedAtUtc = DateTime.UtcNow;
			await dbContext.SaveChangesAsync(cancellationToken);
		}
	}

	/* tail 类型的 schedule 需要额外做一次查表，因为 raw 日志通常按 hex code 识别。 */
	private async Task<string> ResolveTargetValueAsync(WatchSchedule schedule, CancellationToken cancellationToken) {
		if (!string.Equals(schedule.TargetType, TrackTargetTypes.Tail, StringComparison.OrdinalIgnoreCase)) {
			return schedule.TargetValue;
		}

		var normalizedTail = schedule.TargetValue.Trim().ToUpperInvariant();
		var mapping = await dbContext.TailHexMappings
			.AsNoTracking()
			.Where(x => x.Tail == normalizedTail && (x.UserId == null || x.UserId == schedule.UserId))
			.OrderByDescending(x => x.UserId == schedule.UserId)
			.ThenByDescending(x => x.UpdatedAtUtc)
			.FirstOrDefaultAsync(cancellationToken);

		return mapping?.Hex ?? normalizedTail;
	}

	private static bool CanArchive(string status)
		=> string.Equals(status, TrackScheduleStatuses.Completed, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, TrackScheduleStatuses.NoData, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, TrackScheduleStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, TrackScheduleStatuses.Failed, StringComparison.OrdinalIgnoreCase);

	/*
	 * 把 API 输入校验放在 schedule 创建附近，避免非法 UTC 日期/时间直接写进数据库。
	 */
	private static NormalizedRequest NormalizeAndValidate(CreateTrackScheduleRequest request) {
		var displayName = request.DisplayName.Trim();
		if (string.IsNullOrWhiteSpace(displayName)) {
			throw new ArgumentException("displayName is required");
		}

		var targetType = request.TargetType.Trim().ToLowerInvariant();
		if (!TrackTargetTypes.Allowed.Contains(targetType)) {
			throw new ArgumentException("targetType must be one of: tail, hex, flight");
		}

		var targetValue = request.TargetValue.Trim();
		if (string.IsNullOrWhiteSpace(targetValue)) {
			throw new ArgumentException("targetValue is required");
		}

		if (!DateOnly.TryParseExact(request.WatchDateUtc, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var watchDateUtc)) {
			throw new ArgumentException("watchDateUtc must be MM/dd/yyyy");
		}

		if (!TimeOnly.TryParseExact(request.StartZulu, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startZulu)) {
			throw new ArgumentException("startZulu must be HH:mm");
		}

		if (!TimeOnly.TryParseExact(request.EndZulu, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endZulu)) {
			throw new ArgumentException("endZulu must be HH:mm");
		}

		if (startZulu >= endZulu) {
			throw new ArgumentException("startZulu must be earlier than endZulu");
		}

		return new NormalizedRequest(displayName, targetType, targetValue, watchDateUtc, startZulu, endZulu);
	}

	/* 批量加载 latest execution，避免列表接口对每条 schedule 额外打一条查询。 */
	private async Task<Dictionary<long, WatchExecution>> LoadLatestExecutionsAsync(
		IReadOnlyList<WatchSchedule> schedules,
		CancellationToken cancellationToken) {
		var executionIds = schedules
			.Where(x => x.LatestExecutionId.HasValue)
			.Select(x => x.LatestExecutionId!.Value)
			.Distinct()
			.ToList();

		if (executionIds.Count == 0) {
			return [];
		}

		var executions = await dbContext.WatchExecutions
			.AsNoTracking()
			.Where(x => executionIds.Contains(x.Id))
			.ToListAsync(cancellationToken);

		return schedules
			.Where(x => x.LatestExecutionId.HasValue)
			.Join(
				executions,
				schedule => schedule.LatestExecutionId!.Value,
				execution => execution.Id,
				(schedule, execution) => new { schedule.Id, execution })
			.ToDictionary(x => x.Id, x => x.execution);
	}

	private static TrackScheduleListItemResponse MapListItem(WatchSchedule schedule, WatchExecution? latestExecution)
		=> new() {
			Id = schedule.Id,
			DisplayName = schedule.DisplayName,
			TargetType = schedule.TargetType,
			TargetValue = schedule.TargetValue,
			WatchDateUtc = schedule.WatchDateUtc.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
			StartZulu = schedule.StartZulu.ToString("HH:mm", CultureInfo.InvariantCulture),
			EndZulu = schedule.EndZulu.ToString("HH:mm", CultureInfo.InvariantCulture),
			Status = schedule.Status,
			CreatedAtUtc = schedule.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
			LatestExecution = latestExecution is null ? null : MapExecution(latestExecution),
		};

	private static TrackScheduleDetailResponse MapDetail(
		WatchSchedule schedule,
		WatchExecution? latestExecution,
		IReadOnlyList<WatchExecution> executions)
		=> new() {
			Id = schedule.Id,
			DisplayName = schedule.DisplayName,
			TargetType = schedule.TargetType,
			TargetValue = schedule.TargetValue,
			WatchDateUtc = schedule.WatchDateUtc.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
			StartZulu = schedule.StartZulu.ToString("HH:mm", CultureInfo.InvariantCulture),
			EndZulu = schedule.EndZulu.ToString("HH:mm", CultureInfo.InvariantCulture),
			Status = schedule.Status,
			CreatedAtUtc = schedule.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
			LatestExecution = latestExecution is null ? null : MapExecution(latestExecution),
			Executions = executions.Select(MapExecution).ToList(),
		};

	private static TrackExecutionResponse MapExecution(WatchExecution execution)
		=> new() {
			Id = execution.Id,
			ScheduleId = execution.ScheduleId,
			Status = execution.Status,
			MatchedPointCount = execution.MatchedPointCount,
			StartedAtUtc = execution.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
			FinishedAtUtc = execution.FinishedAtUtc?.ToString("O", CultureInfo.InvariantCulture),
			ErrorMessage = execution.ErrorMessage,
			DownloadUrl = string.IsNullOrWhiteSpace(execution.OutputKmlPath)
				? null
				: $"/api/v1/adsb/flights/track-schedules/executions/{execution.Id}/download",
		};

	private sealed record NormalizedRequest(
		string DisplayName,
		string TargetType,
		string TargetValue,
		DateOnly WatchDateUtc,
		TimeOnly StartZulu,
		TimeOnly EndZulu);
}
