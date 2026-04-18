using System.Drawing;
using System.Drawing.Drawing2D;

namespace Snapboard.Helpers;

/// <summary>
/// Fast approximate blur using downscale + bilinear upscale. Cheap, good enough
/// to make text unreadable which is what the blur tool is for.
/// </summary>
public static class BlurHelper
{
    /// <summary>
    /// Produces a blurred copy of <paramref name="source"/>. Higher <paramref name="strength"/>
    /// means more blur (1 = light, 10 = heavy). The returned bitmap is the same pixel size
    /// as the source so it can be used as a one-to-one overlay.
    /// </summary>
    public static Bitmap CreateBlurred(Bitmap source, int strength = 8)
    {
        if (strength < 1) strength = 1;
        if (strength > 20) strength = 20;

        int w = Math.Max(1, source.Width / strength);
        int h = Math.Max(1, source.Height / strength);

        var small = new Bitmap(w, h);
        try
        {
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, w, h));
            }

            var result = new Bitmap(source.Width, source.Height);
            using (var g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(small, new Rectangle(0, 0, source.Width, source.Height));
            }
            return result;
        }
        finally
        {
            small.Dispose();
        }
    }
}
