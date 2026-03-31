using ADSB.Tracker.Server.Dtos.LiveAircraft;
using ADSB.Tracker.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADSB.Tracker.Server.Controllers;

[ApiController]
[Route("adsb/flights/live-aircraft")]
/*
 * 这是一个很小的实时接口，通过本服务把 Ubuntu feeder 的快照转发出来。
 * 这条链路完全不涉及 MySQL 里的 schedule 状态。
 */
public sealed class LiveAircraftController(FeederLiveAircraftService feederLiveAircraftService) : ControllerBase {
	/* 返回 feeder client 当前抓到的一份实时快照。 */
	[HttpGet]
	public async Task<ActionResult<LiveAircraftResponse>> Get(CancellationToken cancellationToken) => Ok(await feederLiveAircraftService.GetSnapshotAsync(cancellationToken));
}