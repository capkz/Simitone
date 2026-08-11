using System.Text.Json.Serialization;

namespace Simitone.iOS.Sync;

/// <summary>Mirrors SimitoneSync.Companion.Manifest.ManifestEntry on the PC side.</summary>
public sealed class ManifestEntry
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = "";

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("lastWriteUtc")]
    public DateTime LastWriteUtc { get; set; }
}
