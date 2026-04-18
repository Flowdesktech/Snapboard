using System.IO;
using System.Windows.Media.Imaging;
using SD = System.Drawing;

namespace Snapboard.Helpers;

public static class BitmapSaver
{
    /// <summary>
    /// Writes a WPF BitmapSource to disk as PNG or JPEG, inferring the encoder
    /// from the file extension when possible (defaults to PNG).
    /// </summary>
    public static void Save(BitmapSource source, string path, int jpegQuality = 92)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        BitmapEncoder encoder = ext switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) },
            _                 => new PngBitmapEncoder(),
        };
        encoder.Frames.Add(BitmapFrame.Create(source));

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    /// <summary>
    /// Writes a GDI bitmap to disk as PNG or JPEG based on extension.
    /// </summary>
    public static void Save(SD.Bitmap bitmap, string path, int jpegQuality = 92)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (ext is ".jpg" or ".jpeg")
        {
            var codec = GetEncoder(SD.Imaging.ImageFormat.Jpeg);
            var prm = new SD.Imaging.EncoderParameters(1);
            prm.Param[0] = new SD.Imaging.EncoderParameter(
                SD.Imaging.Encoder.Quality, (long)Math.Clamp(jpegQuality, 1, 100));
            bitmap.Save(path, codec, prm);
        }
        else
        {
            bitmap.Save(path, SD.Imaging.ImageFormat.Png);
        }
    }

    public static string BuildDefaultFileName(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return $"Snapboard-{DateTime.Now:yyyyMMdd-HHmmss}{ext}";
    }

    private static SD.Imaging.ImageCodecInfo GetEncoder(SD.Imaging.ImageFormat format)
    {
        foreach (var codec in SD.Imaging.ImageCodecInfo.GetImageEncoders())
        {
            if (codec.FormatID == format.Guid) return codec;
        }
        throw new InvalidOperationException($"No encoder for {format}");
    }
}
