using ADSB.Tracker.Server.Dtos.TrackSchedules;
using ADSB.Tracker.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADSB.Tracker.Server.Controllers;

[ApiController]
[Route("adsb/flights/track-schedules")]
/// <summary>
/// HTTP facade for the scheduled-export subsystem.
/// It stays thin on purpose and delegates all domain behavior to TrackScheduleService.
/// </summary>
public sealed class TrackSchedulesController(TrackScheduleService trackScheduleService) : ControllerBase {
	/// <summary>
	/// Create a new schedule definition.
	/// </summary>
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

	/// <summary>
	/// List the caller's active schedules.
	/// </summary>
	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<TrackScheduleListItemResponse>>> List(CancellationToken cancellationToken) => Ok(await trackScheduleService.ListAsync(RequireUserId(), cancellationToken));

	/// <summary>
	/// Get one schedule and its execution history.
	/// </summary>
	[HttpGet("{id:long}")]
	public async Task<ActionResult<TrackScheduleDetailResponse>> GetById(long id, CancellationToken cancellationToken) {
		var item = await trackScheduleService.GetAsync(RequireUserId(), id, cancellationToken);
		return item is null ? NotFound() : Ok(item);
	}

	/// <summary>
	/// Cancel a schedule before it starts.
	/// </summary>
	[HttpPost("{id:long}/cancel")]
	public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken) => await trackScheduleService.CancelAsync(RequireUserId(), id, cancellationToken) ? NoContent() : NotFound();

	/// <summary>
	/// Soft-delete a finished schedule from the default UI list.
	/// </summary>
	[HttpPost("{id:long}/archive")]
	public async Task<IActionResult> Archive(long id, CancellationToken cancellationToken) => await trackScheduleService.ArchiveAsync(RequireUserId(), id, cancellationToken) ? NoContent() : NotFound();

	/// <summary>
	/// List concrete execution attempts for one schedule.
	/// </summary>
	[HttpGet("{id:long}/executions")]
	public async Task<ActionResult<IReadOnlyList<TrackExecutionResponse>>> ListExecutions(long id, CancellationToken cancellationToken) {
		var executions = await trackScheduleService.ListExecutionsAsync(RequireUserId(), id, cancellationToken);
		return executions is null ? NotFound() : Ok(executions);
	}

	/// <summary>
	/// Download the KML export produced by a completed execution.
	/// </summary>
	[HttpGet("executions/{executionId:long}/download")]
	public async Task<IActionResult> DownloadExecution(long executionId, CancellationToken cancellationToken) {
		var file = await trackScheduleService.GetExecutionDownloadAsync(RequireUserId(), executionId, cancellationToken);
		if (file is null) {
			return NotFound();
		}

		return PhysicalFile(file.Value.FilePath, "application/vnd.google-earth.kml+xml", file.Value.DownloadName);
	}

	/// <summary>
	/// This service trusts its upstream caller to tell it which user scope is being acted on.
	/// Flight-Training-Server provides this header when proxying authenticated requests.
	/// </summary>
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
