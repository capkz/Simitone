using System.Text;
using Foundation;

namespace Simitone.iOS.Sync;

/// <summary>
/// Finds PC Companion instances on the local network via Bonjour/mDNS. See
/// docs/pc-transfer-protocol.md in the main repo for the service contract.
///
/// NOTE: written against the documented Foundation.NSNetServiceBrowser /
/// NSNetService API surface, but never compiled - the .NET iOS workload
/// cannot even be installed outside macOS, so this needs a first real build
/// on a Mac to shake out any binding-signature mistakes.
/// </summary>
public sealed class BonjourBrowser : IDisposable
{
    public const string ServiceType = "_simitone-sync._tcp.";
    private const string Domain = "local.";
    private const double ResolveTimeoutSeconds = 5.0;

    private readonly NSNetServiceBrowser _browser = new();

    // NSNetService instances must be kept alive (strongly referenced) for the
    // duration of resolution, or they can be garbage collected mid-resolve.
    private readonly Dictionary<string, DiscoveredPc> _byServiceKey = new();

    public event Action<DiscoveredPc>? PcDiscovered;
    public event Action<DiscoveredPc>? PcUpdated;
    public event Action<DiscoveredPc>? PcLost;

    public BonjourBrowser()
    {
        _browser.FoundService += OnFoundService;
        _browser.ServiceRemoved += OnServiceRemoved;
    }

    public void Start() => _browser.SearchForServices(ServiceType, Domain);

    public void Stop() => _browser.Stop();

    private void OnFoundService(object? sender, NSNetServiceEventArgs e)
    {
        var service = e.Service;
        var key = ServiceKey(service);

        var pc = new DiscoveredPc { Service = service, Name = service.Name };
        _byServiceKey[key] = pc;

        service.AddressResolved += (_, _) => OnResolved(pc);
        service.ResolveFailure += (_, _) => { /* leave unresolved; user can retry */ };
        service.Resolve(ResolveTimeoutSeconds);

        PcDiscovered?.Invoke(pc);
    }

    private void OnResolved(DiscoveredPc pc)
    {
        pc.HostName = pc.Service.HostName;
        pc.Port = (ushort)pc.Service.Port;

        var txtData = pc.Service.GetTxtRecordData();
        if (txtData is not null)
        {
            var txt = NSNetService.DictionaryFromTxtRecord(txtData);
            pc.FileCount = (int)ReadLongProperty(txt, "fileCount");
            // totalBytes routinely exceeds int32 range for a multi-GB install
            // (e.g. 3 GB = 3,221,225,472 > int.MaxValue) - must stay a long.
            pc.TotalBytes = ReadLongProperty(txt, "totalBytes");
        }

        pc.IsResolved = true;
        PcUpdated?.Invoke(pc);
    }

    private static long ReadLongProperty(NSDictionary txt, string key)
    {
        var nsKey = new NSString(key);
        if (txt[nsKey] is NSData data)
        {
            var text = Encoding.UTF8.GetString(data.ToArray());
            if (long.TryParse(text, out var value))
                return value;
        }

        return 0;
    }

    private void OnServiceRemoved(object? sender, NSNetServiceEventArgs e)
    {
        var key = ServiceKey(e.Service);
        if (_byServiceKey.Remove(key, out var pc))
            PcLost?.Invoke(pc);
    }

    private static string ServiceKey(NSNetService service) => $"{service.Name}.{service.Type}{service.Domain}";

    public void Dispose()
    {
        _browser.Stop();
        _browser.Dispose();
    }
}
