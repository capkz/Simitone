using Foundation;

namespace Simitone.iOS.Sync;

public sealed class DiscoveredPc
{
    public required NSNetService Service { get; init; }
    public required string Name { get; init; }
    public string? HostName { get; set; }
    public ushort Port { get; set; }
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public bool IsResolved { get; set; }
}
