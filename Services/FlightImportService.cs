using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ADSB.Tracker.Server.Data.Entities;
using ADSB.Tracker.Server.Options;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Services;

/*
 * 这个服务负责在 schedule 成功导出 KML 之后，
 * 把导出结果通过 HTTP 回调给 Flight-Training-Server。
 *
 * 它的作用是维持服务边界：
 * - ADSB-Tracker-Server 只负责 schedule / execution / KML 导出
 * - Flight-Training-Server 负责把这份轨迹落到 flight / flight_track 业务模型里
 */
public sealed class FlightImportService(HttpClient httpClient, IOptions<FlightTrainingServerOptions> options, IConfiguration configuration) {
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/*
	 * 当 execution 已经产出 KML 后，从本地读取 KML 内容，
	 * 组装成内部导入请求，再发给主后端的内部接口。
	 */
	public async Task ImportScheduledTrackAsync(
		WatchSchedule schedule,
		WatchExecution execution,
		CancellationToken cancellationToken) {
		/*
		 * 允许优先走环境变量，其次走 Options 配置。
		 * 如果没有配置主后端地址，就直接跳过回调。
		 */
		var baseUrl = ResolveValue(
			configuration["FLIGHT_TRAINING_SERVER_BASE_URL"],
			options.Value.BaseUrl);
		if (string.IsNullOrWhiteSpace(baseUrl)) {
			return;
		}

		if (string.IsNullOrWhiteSpace(execution.OutputKmlPath) || !File.Exists(execution.OutputKmlPath)) {
			return;
		}

		/*
		 * 主后端需要的不只是“导出成功了”这个事实，
		 * 而是整份 KML 内容和这次 execution 的上下文信息。
		 */
		var rawKml = await File.ReadAllTextAsync(execution.OutputKmlPath, cancellationToken);
		
		// 建立对另一个服务器的Request，组装 payload
		using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/flight/internal/track-schedule-import");
		var payload = new {
			UserId = schedule.UserId,
			ScheduleId = schedule.Id,
			ExecutionId = execution.Id,
			DisplayName = schedule.DisplayName,
			TargetType = schedule.TargetType,
			TargetValue = schedule.TargetValue,
			RawKml = rawKml,
			RawFilename = Path.GetFileName(execution.OutputKmlPath),
		};
		
		/*
		 * 这是服务到服务的内部调用。
		 * 如果配置了 service token，就放到请求头里让主后端校验。
		 */
		var serviceToken = ResolveValue(
			configuration["FLIGHT_TRAINING_SERVER_SERVICE_TOKEN"],
			options.Value.ServiceToken);
		if (!string.IsNullOrWhiteSpace(serviceToken)) {
			request.Headers.TryAddWithoutValidation("X-Service-Token", serviceToken);
		}

		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		request.Content = new StringContent(
			JsonSerializer.Serialize(payload, JsonOptions),
			Encoding.UTF8,
			"application/json");

		using var response = await httpClient.SendAsync(request, cancellationToken);
		if (response.IsSuccessStatusCode) {
			return;
		}

		/*
		 * 这里抛异常的意义不是“导出失败”，
		 * 而是“导出成功了，但导入主后端失败了”。
		 * 上层会单独记录这类错误日志。
		 */
		var raw = await response.Content.ReadAsStringAsync(cancellationToken);
		throw new InvalidOperationException(
			$"Flight import callback failed ({(int)response.StatusCode}): {raw}");
	}

	/* 在环境变量和配置文件之间做一个简单的兜底选择。 */
	private static string ResolveValue(string? preferred, string fallback) => string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();
}
