namespace ADSB.Tracker.Server.Options;

public sealed class PiTrackSourceOptions
{
    public const string SectionName = "PiTrackSource";
    public string Mode { get; set; } = "local";
    public string RawRootPath { get; set; } = string.Empty;
    public string SshHost { get; set; } = string.Empty;
    public string SshUser { get; set; } = string.Empty;
    public int SshPort { get; set; } = 22;
    public string RemoteRawRootPath { get; set; } = string.Empty;
    public string SshIdentityFile { get; set; } = string.Empty;
    public bool SshAcceptNewHostKey { get; set; } = true;
}
