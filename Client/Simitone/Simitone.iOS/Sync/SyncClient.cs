using System.Text.Json;

namespace Simitone.iOS.Sync;

public sealed record SyncProgress(int FilesDone, int FilesTotal, long BytesDone, long BytesTotal, string CurrentFile);

/// <summary>
/// Talks to the PC Companion's HTTP API (see docs/pc-transfer-protocol.md) to pull
/// changed/new game files down into GameFilesStore.RootPath.
/// </summary>
public sealed class SyncClient
{
    private readonly HttpClient _http;
    private readonly string _pin;

    public SyncClient(string hostName, int port, string pin)
    {
        _pin = pin;
        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://{hostName}:{port}/"),
            Timeout = TimeSpan.FromMinutes(10),
        };
        _http.DefaultRequestHeaders.Add("X-Simitone-Pin", pin);
    }

    public async Task<IReadOnlyList<ManifestEntry>> FetchManifestAsync(CancellationToken ct)
    {
        var json = await _http.GetStringAsync("api/manifest", ct);
        return JsonSerializer.Deserialize<List<ManifestEntry>>(json, JsonOptions) ?? [];
    }

    /// <summary>
    /// Downloads every entry that's missing or differs from the last synced
    /// manifest (by size + last-write time - see ManifestEntry doc comment on the
    /// PC side for why content isn't hashed) into GameFilesStore.RootPath.
    /// </summary>
    public async Task SyncAsync(IProgress<SyncProgress>? progress, CancellationToken ct)
    {
        var remoteManifest = await FetchManifestAsync(ct);
        var previous = LoadCachedManifest();

        var toDownload = remoteManifest
            .Where(entry => !previous.TryGetValue(entry.RelativePath, out var prev) || HasChanged(prev, entry))
            .ToList();

        var totalBytes = toDownload.Sum(e => e.SizeBytes);
        long bytesDone = 0;

        for (var i = 0; i < toDownload.Count; i++)
        {
            var entry = toDownload[i];
            progress?.Report(new SyncProgress(i, toDownload.Count, bytesDone, totalBytes, entry.RelativePath));

            await DownloadFileAsync(entry, ct);
            bytesDone += entry.SizeBytes;
        }

        progress?.Report(new SyncProgress(toDownload.Count, toDownload.Count, bytesDone, totalBytes, ""));

        GameFilesStore.WriteCachedManifestJson(JsonSerializer.Serialize(remoteManifest, JsonOptions));
        GameFilesStore.MarkSyncComplete();
    }

    private async Task DownloadFileAsync(ManifestEntry entry, CancellationToken ct)
    {
        var localPath = Path.Combine(GameFilesStore.RootPath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        // relativePath segments are escaped individually so any '/' inside a
        // segment (there shouldn't be any, but a stray one must not be read as a
        // path separator) doesn't get misinterpreted, while the separators
        // between segments are preserved.
        var encodedPath = string.Join('/', entry.RelativePath.Split('/').Select(Uri.EscapeDataString));

        using var response = await _http.GetAsync($"api/files/{encodedPath}", HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var remoteStream = await response.Content.ReadAsStreamAsync(ct);
        await using var localStream = File.Create(localPath);
        await remoteStream.CopyToAsync(localStream, ct);
    }

    private static bool HasChanged(ManifestEntry previous, ManifestEntry current) =>
        previous.SizeBytes != current.SizeBytes || previous.LastWriteUtc != current.LastWriteUtc;

    private static Dictionary<string, ManifestEntry> LoadCachedManifest()
    {
        var json = GameFilesStore.ReadCachedManifestJson();
        if (json is null)
            return [];

        var entries = JsonSerializer.Deserialize<List<ManifestEntry>>(json, JsonOptions) ?? [];
        return entries.ToDictionary(e => e.RelativePath);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
