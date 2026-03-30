using ADSB.Tracker.Server.Options;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Services;

public sealed class PiTrackSourceService(
    IOptions<PiTrackSourceOptions> sourceOptions,
    IOptions<TrackerStorageOptions> storageOptions)
{
    public async Task<(string RemotePath, string LocalPath)?> FetchRawFileAsync(
        DateOnly watchDateUtc,
        long executionId,
        CancellationToken cancellationToken)
    {
        var rawRoot = sourceOptions.Value.RawRootPath;
        if (string.IsNullOrWhiteSpace(rawRoot))
        {
            return null;
        }

        var fileName = $"{watchDateUtc:yyyy-MM-dd}.jsonl";
        var sourcePath = Path.Combine(rawRoot, fileName);
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        var workDirectory = Path.Combine(
            storageOptions.Value.WorkingDirectory,
            "executions",
            executionId.ToString());
        Directory.CreateDirectory(workDirectory);

        var destinationPath = Path.Combine(workDirectory, fileName);
        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);

        return (sourcePath, destinationPath);
    }
}
