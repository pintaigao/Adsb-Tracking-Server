using ADSB.Tracker.Server.Dtos.LiveAircraft;
using ADSB.Tracker.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADSB.Tracker.Server.Controllers;

[ApiController]
[Route("adsb/flights/live-aircraft")]
/*
 * Small real-time endpoint that exposes the Ubuntu feeder snapshot through this service.
 * No MySQL schedule state is involved here.
 */
public sealed class LiveAircraftController(FeederLiveAircraftService feederLiveAircraftService)
    : ControllerBase
{
    /* Return one current snapshot from the feeder client. */
    [HttpGet]
    public async Task<ActionResult<LiveAircraftResponse>> Get(CancellationToken cancellationToken)
        => Ok(await feederLiveAircraftService.GetSnapshotAsync(cancellationToken));
}
