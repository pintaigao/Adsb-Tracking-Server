using System.Globalization;
using System.Text;
using System.Text.Json;
using ADSB.Tracker.Server.Constants;
using ADSB.Tracker.Server.Models;
using ADSB.Tracker.Server.Options;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Services;

/*
 * 这个服务把某一天的原始 jsonl 日志过滤成某条 schedule 对应的轨迹点，
 * 然后写出 gx:Track 格式的 KML。
 */
public sealed class TrackExportService(IOptions<TrackerStorageOptions> storageOptions) {
	/* TrackScheduleService 在拿到本地 raw 文件之后，会从这里进入导出流程。 */
	public async Task<(int MatchedPointCount, string? OutputPath)> ExportAsync(string rawPath, string targetType, string targetValue, string displayName, DateOnly watchDateUtc, TimeOnly startZulu, TimeOnly endZulu, long executionId, CancellationToken cancellationToken) {
		var points = await LoadFilteredPointsAsync(rawPath, targetType, targetValue, watchDateUtc, startZulu, endZulu, cancellationToken);

		if (points.Count < 2) {
			return (points.Count, null);
		}

		var exportRoot = storageOptions.Value.ExportDirectory;
		Directory.CreateDirectory(exportRoot);

		var fileName = $"{SanitizeFileName(displayName)}-{watchDateUtc:yyyy-MM-dd}-{executionId}.kml";
		var outputPath = Path.Combine(exportRoot, fileName);
		await File.WriteAllTextAsync(outputPath, BuildKml(displayName, targetValue, points), cancellationToken);

		return (points.Count, outputPath);
	}

	/*
	 * 按行流式读取 jsonl，这样即使一天的 raw 文件很大，也不用一次性全部读进内存。
	 */
	private static async Task<List<RawTrackPoint>> LoadFilteredPointsAsync(string rawPath, string targetType, string targetValue, DateOnly watchDateUtc, TimeOnly startZulu, TimeOnly endZulu, CancellationToken cancellationToken) {
		var points = new List<RawTrackPoint>();
		await using var stream = File.OpenRead(rawPath);
		using var reader = new StreamReader(stream);

		while (!reader.EndOfStream) {
			cancellationToken.ThrowIfCancellationRequested();
			var line = await reader.ReadLineAsync(cancellationToken);
			if (string.IsNullOrWhiteSpace(line)) {
				continue;
			}

			RawTrackPoint? point;
			try {
				point = JsonSerializer.Deserialize<RawTrackPoint>(line);
			} catch (JsonException) {
				continue;
			}

			if (point is null || point.Lat is null || point.Lon is null) {
				continue;
			}

			if (!MatchesWindow(point.Ts, watchDateUtc, startZulu, endZulu)) {
				continue;
			}

			if (!MatchesTarget(point, targetType, targetValue)) {
				continue;
			}

			points.Add(point);
		}

		points.Sort((a, b) => a.Ts.CompareTo(b.Ts));
		return points;
	}

	private static bool MatchesWindow(double ts, DateOnly watchDateUtc, TimeOnly startZulu, TimeOnly endZulu) {
		var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(ts * 1000d));
		if (DateOnly.FromDateTime(timestamp.UtcDateTime) != watchDateUtc) {
			return false;
		}

		var utcTime = TimeOnly.FromDateTime(timestamp.UtcDateTime);
		return utcTime >= startZulu && utcTime <= endZulu;
	}

	private static bool MatchesTarget(RawTrackPoint point, string targetType, string targetValue) {
		var normalizedTarget = Normalize(targetValue);
		return targetType.ToLowerInvariant() switch {
			TrackTargetTypes.Hex => Normalize(point.Hex) == normalizedTarget,
			TrackTargetTypes.Flight => Normalize(point.Flight) == normalizedTarget,
			TrackTargetTypes.Tail => Normalize(point.Hex) == normalizedTarget || Normalize(point.Flight) == normalizedTarget,
			_ => false,
		};
	}

	private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

	private static string SanitizeFileName(string value) {
		var invalid = Path.GetInvalidFileNameChars();
		var builder = new StringBuilder(value.Length);
		foreach (var ch in value) {
			builder.Append(invalid.Contains(ch) ? '_' : ch);
		}

		var sanitized = builder.ToString().Trim();
		return string.IsNullOrWhiteSpace(sanitized) ? "track" : sanitized;
	}

	/*
	 * 这里拼出最终的 KML 内容，后面既可以直接下载，也可以导入到 Flight-Training。
	 */
	private static string BuildKml(string displayName, string targetValue, IReadOnlyList<RawTrackPoint> points) {
		var startUtc = FormatUtc(points[0].Ts);
		var endUtc = FormatUtc(points[^1].Ts);
		var flight = points
			.Select(point => point.Flight)
			.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
			?.Trim() ?? string.Empty;
		var hex = points
			.Select(point => point.Hex)
			.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
			?.Trim() ?? string.Empty;
		var squawk = points
			.Select(point => point.Squawk)
			.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
			?.Trim() ?? string.Empty;
		var trackRows = string.Join(
			Environment.NewLine,
			points.Select(point => {
				var altitudeMeters = Math.Round(((point.Alt ?? 0d) * 0.3048d), 1);
				var timestamp = FormatUtc(point.Ts);
				var coord = FormattableString.Invariant($"{point.Lon} {point.Lat} {altitudeMeters}");
				return $$"""
				                   <when>{{timestamp}}</when>
				                   <gx:coord>{{coord}}</gx:coord>
				         """;
			}));

		static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;

		var description = Escape($"Track name: {displayName}\nFlight: {flight}\nHex: {hex}\nSquawk: {squawk}\nTarget: {targetValue}\nStart: {startUtc}\nEnd: {endUtc}\nPoints: {points.Count}");

		return $$"""
		         <?xml version="1.0" encoding="UTF-8"?>
		         <kml xmlns="http://www.opengis.net/kml/2.2" xmlns:gx="http://www.google.com/kml/ext/2.2">
		           <Document>
		             <open>1</open>
		             <visibility>1</visibility>
		             <Style id="trackStyle">
		               <LineStyle>
		                 <color>ff00a5ff</color>
		                 <width>3</width>
		               </LineStyle>
		             </Style>
		             <name>{{Escape(displayName)}}</name>
		             <Placemark>
		               <name>{{Escape(displayName)}}</name>
		               <description>{{description}}</description>
		               <styleUrl>#trackStyle</styleUrl>
		               <gx:Track>
		                 <altitudeMode>absolute</altitudeMode>
		                 <extrude>1</extrude>
		                 <altitudeMode>absolute</altitudeMode>
		                 <gx:interpolate>1</gx:interpolate>
		                 {{trackRows}}
		               </gx:Track>
		             </Placemark>
		           </Document>
		         </kml>
		         """;
	}

	private static string FormatUtc(double ts) {
		var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(ts * 1000d));
		return timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
	}
}