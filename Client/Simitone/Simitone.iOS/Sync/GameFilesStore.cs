namespace Simitone.iOS.Sync;

/// <summary>
/// Where synced game files live on-device, and whether a sync has completed.
/// Deliberately inside the app's own sandboxed container (Documents/) rather
/// than anywhere a Files-app document picker would be needed - none of this
/// data needs to be user-visible outside the app.
/// </summary>
public static class GameFilesStore
{
    public static string RootPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameFiles");

    private static string ManifestCachePath => Path.Combine(RootPath, ".last-sync-manifest.json");
    private static string CompleteMarkerPath => Path.Combine(RootPath, ".sync-complete");

    /// <summary>Returns the game path if a sync has completed before, otherwise null.</summary>
    public static string? ResolveGamePath() => File.Exists(CompleteMarkerPath) ? RootPath : null;

    public static void MarkSyncComplete()
    {
        Directory.CreateDirectory(RootPath);
        File.WriteAllText(CompleteMarkerPath, DateTimeOffset.UtcNow.ToString("O"));
    }

    public static string? ReadCachedManifestJson() =>
        File.Exists(ManifestCachePath) ? File.ReadAllText(ManifestCachePath) : null;

    public static void WriteCachedManifestJson(string json)
    {
        Directory.CreateDirectory(RootPath);
        File.WriteAllText(ManifestCachePath, json);
    }
}
