using ADSB.Tracker.Server.Contracts.TrackSchedules;
using ADSB.Tracker.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADSB.Tracker.Server.Controllers;

[ApiController]
[Route("adsb/flights/track-schedules")]
public sealed class TrackSchedulesController(TrackScheduleService trackScheduleService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TrackScheduleDetailResponse>> Create(
        [FromBody] CreateTrackScheduleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await trackScheduleService.CreateAsync(RequireUserId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrackScheduleListItemResponse>>> List(
        CancellationToken cancellationToken)
        => Ok(await trackScheduleService.ListAsync(RequireUserId(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<TrackScheduleDetailResponse>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var item = await trackScheduleService.GetAsync(RequireUserId(), id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
        => await trackScheduleService.CancelAsync(RequireUserId(), id, cancellationToken)
            ? NoContent()
            : NotFound();

    [HttpGet("{id:long}/executions")]
    public async Task<ActionResult<IReadOnlyList<TrackExecutionResponse>>> ListExecutions(
        long id,
        CancellationToken cancellationToken)
    {
        var executions = await trackScheduleService.ListExecutionsAsync(RequireUserId(), id, cancellationToken);
        return executions is null ? NotFound() : Ok(executions);
    }

    [HttpGet("executions/{executionId:long}/download")]
    public async Task<IActionResult> DownloadExecution(
        long executionId,
        CancellationToken cancellationToken)
    {
        var file = await trackScheduleService.GetExecutionDownloadAsync(RequireUserId(), executionId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return PhysicalFile(
            file.Value.FilePath,
            "application/vnd.google-earth.kml+xml",
            file.Value.DownloadName);
    }

    private string RequireUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var values))
        {
            var userId = values.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }
        }

        throw new BadHttpRequestException(
            "Missing X-User-Id header.",
            StatusCodes.Status401Unauthorized);
    }
}
