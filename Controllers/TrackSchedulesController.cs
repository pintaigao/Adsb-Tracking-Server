using ADSB.Tracker.Server.Dtos.TrackSchedules;
using ADSB.Tracker.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADSB.Tracker.Server.Controllers;

[ApiController]
[Route("adsb/flights/track-schedules")]
/*
 * 这是 schedule 导出子系统的 HTTP 外观层。
 * 它故意保持很薄，真正的领域逻辑都交给 TrackScheduleService。
 */
public sealed class TrackSchedulesController(TrackScheduleService trackScheduleService) : ControllerBase {
	/* 创建一条新的 schedule 定义。 */
	[HttpPost]
	public async Task<ActionResult<TrackScheduleDetailResponse>> Create([FromBody] CreateTrackScheduleRequest request, CancellationToken cancellationToken) {
		try {
			var created = await trackScheduleService.CreateAsync(RequireUserId(), request, cancellationToken);
			return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
		}
		catch (ArgumentException ex) {
			return ValidationProblem(detail: ex.Message);
		}
	}

	/* 列出当前调用者的活跃 schedule。 */
	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<TrackScheduleListItemResponse>>> List(CancellationToken cancellationToken) => Ok(await trackScheduleService.ListAsync(RequireUserId(), cancellationToken));

	/* 读取单条 schedule 及其执行历史。 */
	[HttpGet("{id:long}")]
	public async Task<ActionResult<TrackScheduleDetailResponse>> GetById(long id, CancellationToken cancellationToken) {
		var item = await trackScheduleService.GetAsync(RequireUserId(), id, cancellationToken);
		return item is null ? NotFound() : Ok(item);
	}

	/* schedule 开始执行前允许取消。 */
	[HttpPost("{id:long}/cancel")]
	public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken) => await trackScheduleService.CancelAsync(RequireUserId(), id, cancellationToken) ? NoContent() : NotFound();

	/* 把已结束 schedule 软删除，使其从默认 UI 列表中消失。 */
	[HttpPost("{id:long}/archive")]
	public async Task<IActionResult> Archive(long id, CancellationToken cancellationToken) => await trackScheduleService.ArchiveAsync(RequireUserId(), id, cancellationToken) ? NoContent() : NotFound();

	/* 列出某条 schedule 的具体 execution 记录。 */
	[HttpGet("{id:long}/executions")]
	public async Task<ActionResult<IReadOnlyList<TrackExecutionResponse>>> ListExecutions(long id, CancellationToken cancellationToken) {
		var executions = await trackScheduleService.ListExecutionsAsync(RequireUserId(), id, cancellationToken);
		return executions is null ? NotFound() : Ok(executions);
	}

	/* 下载某次已完成 execution 生成的 KML。 */
	[HttpGet("executions/{executionId:long}/download")]
	public async Task<IActionResult> DownloadExecution(long executionId, CancellationToken cancellationToken) {
		var file = await trackScheduleService.GetExecutionDownloadAsync(RequireUserId(), executionId, cancellationToken);
		if (file is null) {
			return NotFound();
		}

		return PhysicalFile(file.Value.FilePath, "application/vnd.google-earth.kml+xml", file.Value.DownloadName);
	}

	/*
	 * 这个服务信任上游调用方告诉它当前操作的是哪个用户。
	 * 当 Flight-Training-Server 代理认证后的请求时，会带上这个头。
	 */
	private string RequireUserId() {
		if (Request.Headers.TryGetValue("X-User-Id", out var values)) {
			var userId = values.FirstOrDefault()?.Trim();
			if (!string.IsNullOrWhiteSpace(userId)) {
				return userId;
			}
		}

		throw new BadHttpRequestException("Missing X-User-Id header.", StatusCodes.Status401Unauthorized);
	}
}
