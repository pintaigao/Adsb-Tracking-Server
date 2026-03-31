using System.Diagnostics;
using ADSB.Tracker.Server.Options;
using Microsoft.Extensions.Options;

namespace ADSB.Tracker.Server.Services;

/*
 * 这个服务负责找到某一天的原始 ADS-B jsonl 文件，
 * 并把它复制到当前 execution 的工作目录里。
 * 来源可以是本机磁盘，也可以是通过 scp 访问的 Ubuntu 远程机器。
 */
public sealed class PiTrackSourceService(
    IOptions<PiTrackSourceOptions> sourceOptions,
    IOptions<TrackerStorageOptions> storageOptions)
{
	/*
	 * 无论源数据原来在本机还是 Ubuntu receiver 上，
	 * 这里都统一返回一个本地工作副本，后面的导出逻辑就不用关心来源差异。
	 */
	public async Task<(string RemotePath, string LocalPath)?> FetchRawFileAsync(
		DateOnly watchDateUtc,
		long executionId,
		CancellationToken cancellationToken)
    {
        var options = sourceOptions.Value;
		var fileName = $"{watchDateUtc:yyyy-MM-dd}.jsonl";
		var workDirectory = Path.Combine(
			storageOptions.Value.WorkingDirectory,
			"executions",
			executionId.ToString());
		Directory.CreateDirectory(workDirectory);

		var destinationPath = Path.Combine(workDirectory, fileName);
        var mode = Normalize(options.Mode);

        return mode switch
        {
            "ssh" => await FetchRemoteOverScpAsync(options, fileName, destinationPath, cancellationToken),
            _ => await FetchLocalAsync(options.RawRootPath, fileName, destinationPath, cancellationToken),
        };
	}

    /* 简单模式：适合服务和原始数据目录在同一台机器上，直接从本地目录复制。 */
    private static async Task<(string RemotePath, string LocalPath)?> FetchLocalAsync(
        string rawRoot,
        string fileName,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRoot))
        {
            return null;
        }

        var sourcePath = Path.Combine(rawRoot, fileName);
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);

        return (sourcePath, destinationPath);
    }

    /* 当前 Mac + Ubuntu 场景：schedule 执行时按需通过 scp 拉取某一天的单个文件。 */
    private static async Task<(string RemotePath, string LocalPath)?> FetchRemoteOverScpAsync(
        PiTrackSourceOptions options,
        string fileName,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.SshHost)
            || string.IsNullOrWhiteSpace(options.SshUser)
            || string.IsNullOrWhiteSpace(options.RemoteRawRootPath))
        {
            throw new InvalidOperationException(
                "PiTrackSource SSH mode requires SshHost, SshUser, and RemoteRawRootPath.");
        }

        var remotePath = $"{options.SshUser}@{options.SshHost}:{PosixCombine(options.RemoteRawRootPath, fileName)}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "scp",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-q");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add("BatchMode=yes");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(
            $"StrictHostKeyChecking={(options.SshAcceptNewHostKey ? "accept-new" : "yes")}");

        if (options.SshPort > 0)
        {
            startInfo.ArgumentList.Add("-P");
            startInfo.ArgumentList.Add(options.SshPort.ToString());
        }

        if (!string.IsNullOrWhiteSpace(options.SshIdentityFile))
        {
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(options.SshIdentityFile);
        }

        startInfo.ArgumentList.Add(remotePath);
        startInfo.ArgumentList.Add(destinationPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start scp process.");

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;
        _ = await stdoutTask;

        if (process.ExitCode == 0 && File.Exists(destinationPath))
        {
            return (remotePath, destinationPath);
        }

        TryDelete(destinationPath);

        if (stderr.Contains("No such file", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        throw new InvalidOperationException(
            $"scp failed for {remotePath} (exit {process.ExitCode}): {stderr.Trim()}");
    }

    private static string PosixCombine(string root, string fileName)
        => $"{root.TrimEnd('/')}/{fileName}";

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore cleanup errors
        }
    }
}
