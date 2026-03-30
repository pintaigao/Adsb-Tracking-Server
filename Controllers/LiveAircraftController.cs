using ADSB.Tracker.Server.Dtos.LiveAircraft;
using ADSB.Tracker.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADSB.Tracker.Server.Controllers;

[ApiController]
[Route("adsb/flights/live-aircraft")]
public sealed class LiveAircraftController(FeederLiveAircraftService feederLiveAircraftService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LiveAircraftResponse>> Get(CancellationToken cancellationToken)
        => Ok(await feederLiveAircraftService.GetSnapshotAsync(cancellationToken));
}
