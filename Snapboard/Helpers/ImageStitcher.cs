using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Snapboard.Helpers;

/// <summary>
/// Stitches a sequence of fixed-size frames captured while the user scrolled
/// through content (scrolling capture). For each consecutive pair the
/// stitcher searches for the vertical overlap that best aligns the top of the
/// new frame with a strip somewhere inside the previous one, then appends the
/// new pixels below.
///
/// This is what lets Snapboard produce a single tall image of a webpage / chat
/// window / log view even though Windows has no native "capture a scrollable
/// control" API.
/// </summary>
public static class ImageStitcher
{
    /// <summary>Stitch frames in order. Returns a new bitmap owned by the
    /// caller. All frames must have identical dimensions.</summary>
    public static Bitmap Stitch(IReadOnlyList<Bitmap> frames)
    {
        if (frames == null || frames.Count == 0)
            throw new ArgumentException("Need at least one frame.", nameof(frames));

        if (frames.Count == 1) return (Bitmap)frames[0].Clone();

        int width  = frames[0].Width;
        int height = frames[0].Height;
        foreach (var f in frames)
        {
            if (f.Width != width || f.Height != height)
                throw new ArgumentException("All frames must have the same dimensions.");
        }

        // Compute each frame's position (top-row in the output) via pairwise
        // overlap detection. Duplicate frames (overlap == full height) are
        // skipped so stationary moments don't produce weird doubled sections.
        var offsets = new int[frames.Count];
        int outHeight = height;
        offsets[0] = 0;

        LockedBitmap? prev = null;
        try
        {
            prev = LockedBitmap.Lock(frames[0]);
            for (int i = 1; i < frames.Count; i++)
            {
                using var cur = LockedBitmap.Lock(frames[i]);
                int overlap = FindBestOverlap(prev, cur);
                if (overlap >= height - 1)
                {
                    // near-identical, skip
                    offsets[i] = int.MinValue;
                    continue;
                }
                offsets[i] = outHeight - overlap;
                outHeight += (height - overlap);
                prev.Dispose();
                prev = LockedBitmap.Lock(frames[i]);
            }
        }
        finally
        {
            prev?.Dispose();
        }

        var result = new Bitmap(width, outHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.Clear(Color.White);
            for (int i = 0; i < frames.Count; i++)
            {
                if (offsets[i] == int.MinValue) continue;
                g.DrawImageUnscaled(frames[i], 0, offsets[i]);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns overlap k (1..H) where frame A's bottom k rows best match
    /// frame B's top k rows. Returns 0 when no confident match was found —
    /// caller appends B in full. Returns H when frames are near-identical —
    /// caller should skip B.
    /// </summary>
    public static int FindBestOverlap(LockedBitmap a, LockedBitmap b)
    {
        int h = a.Height;
        int w = a.Width;
        if (h < 8 || w < 8) return 0;

        // Sample a small strip from the top of B and slide it against A.
        const int stripRows = 14;
        int minOverlap = Math.Max(stripRows, h / 24);
        int maxOverlap = h - 2;

        // Sample every Nth column for speed; with 1920px wide frames this
        // gives ~64 samples per row which is plenty for correlation.
        int colStep = Math.Max(1, w / 64);
        int colSamples = 0;
        for (int x = 0; x < w; x += colStep) colSamples++;
        if (colSamples == 0) return 0;
        int samplesPerStrip = stripRows * colSamples;

        long bestError = long.MaxValue;
        int bestK = 0;

        // Walk from large overlaps (small scroll delta) down to small overlaps
        // (big scroll delta) so near-stationary frames get detected first.
        for (int k = maxOverlap; k >= minOverlap; k--)
        {
            long error = 0;
            long budget = bestError;
            for (int sy = 0; sy < stripRows; sy++)
            {
                int aY = h - k + sy;
                if (aY < 0 || aY >= h) { error = long.MaxValue; break; }
                for (int x = 0; x < w; x += colStep)
                {
                    int pa = a.GetPixel(x, aY);
                    int pb = b.GetPixel(x, sy);
                    error += PixelSqError(pa, pb);
                    if (error >= budget) break;
                }
                if (error >= budget) break;
            }
            if (error < bestError)
            {
                bestError = error;
                bestK = k;
            }
        }

        if (bestK == 0) return 0;

        double avgError = (double)bestError / samplesPerStrip;
        // Empirical threshold: ~900 ≈ avg per-channel diff of ~17 on each RGB.
        // Generous enough for anti-aliased text but rejects pure noise.
        if (avgError > 900) return 0;

        // If frames are near-identical (max overlap gave near-zero error),
        // collapse to "full overlap, skip new frame".
        if (bestK >= maxOverlap && avgError < 40) return h;
        return bestK;
    }

    private static int PixelSqError(int a, int b)
    {
        int dr = ((a >> 16) & 0xFF) - ((b >> 16) & 0xFF);
        int dg = ((a >>  8) & 0xFF) - ((b >>  8) & 0xFF);
        int db = ( a        & 0xFF) - ( b        & 0xFF);
        return dr * dr + dg * dg + db * db;
    }

    // ------------------------------------------------------------------

    /// <summary>Lightweight copy-out of a bitmap's pixels into an int[] buffer
    /// so our overlap scan doesn't pay LockBits/UnlockBits cost per access
    /// and stays managed-only (no unsafe block needed).</summary>
    public sealed class LockedBitmap : IDisposable
    {
        private readonly int[] _pixels;
        public int Width  { get; }
        public int Height { get; }
        public int Stride { get; } // in ints (1 int == 1 ARGB pixel for 32bpp)

        private LockedBitmap(int[] pixels, int width, int height, int strideInts)
        {
            _pixels = pixels;
            Width = width;
            Height = height;
            Stride = strideInts;
        }

        public static LockedBitmap Lock(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int strideInts = data.Stride / 4;
                int len = strideInts * data.Height;
                var buf = new int[len];
                Marshal.Copy(data.Scan0, buf, 0, len);
                return new LockedBitmap(buf, bmp.Width, bmp.Height, strideInts);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        public int GetPixel(int x, int y) => _pixels[y * Stride + x];

        public void Dispose() { /* nothing — buffer is managed */ }
    }
}
