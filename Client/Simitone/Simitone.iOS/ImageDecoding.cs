using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Simitone.iOS;

/// <summary>
/// Same ImageSharp-based decoding Simitone.Desktop uses (see its Program.cs
/// BitmapReader) - pure managed code, no native image APIs, so it behaves
/// identically across desktop and iOS instead of needing a separate
/// UIImage-based path.
/// </summary>
public static class ImageDecoding
{
    public static Tuple<byte[], int, int> BitmapReader(Stream stream)
    {
        using var image = Image.Load<Rgba32>(stream);
        var data = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(data);
        return new Tuple<byte[], int, int>(data, image.Width, image.Height);
    }
}
