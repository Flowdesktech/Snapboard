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

                // Defensive dedup: the capture loop already drops near-
                // identical frames, but this catches anything that slips
                // through (e.g. if idle detection was borderline).
                if (AreFramesNearIdentical(prev, cur))
                {
                    offsets[i] = int.MinValue;
                    continue;
                }

                int overlap = FindBestOverlap(prev, cur);
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
    /// Returns overlap <c>k</c> (0..H-2) where frame A's bottom <c>k</c> rows
    /// best match frame B's top <c>k</c> rows. Returns <c>0</c> when no
    /// confident match was found — caller should append B in full.
    ///
    /// Sampling strategy: we correlate TWO strips inside B (a top one and a
    /// middle one) against A at each candidate <c>k</c>. A single top strip
    /// is too easy to fool on content with fixed headers / whitespace /
    /// uniform backgrounds — it can match A at many positions equally well,
    /// and the loop locks onto the *first* (which is always "barely
    /// scrolled"). Requiring the middle strip to ALSO align at the same
    /// <c>k</c> eliminates that ambiguity: the middle of a viewport almost
    /// always contains unique content.
    /// </summary>
    public static int FindBestOverlap(LockedBitmap a, LockedBitmap b)
    {
        int h = a.Height;
        int w = a.Width;
        if (h < 32 || w < 8) return 0;

        const int stripRows = 14;
        int topStrip    = 0;
        int middleStrip = h / 3;

        // Sample every Nth column for speed — with 1920 px wide frames this
        // still gives ~64 samples per row, plenty for correlation.
        int colStep = Math.Max(1, w / 64);
        int colSamples = 0;
        for (int x = 0; x < w; x += colStep) colSamples++;
        if (colSamples == 0) return 0;
        int samplesTotal = stripRows * 2 * colSamples;

        // Both strips must fit inside the overlap region in B (rows 0..k-1),
        // so k must be at least `middleStrip + stripRows`.
        int minOverlap = Math.Max(middleStrip + stripRows, h / 24);
        int maxOverlap = h - 2;
        if (minOverlap >= maxOverlap) return 0;

        Span<int> bStarts = stackalloc int[] { topStrip, middleStrip };

        long bestError = long.MaxValue;
        int bestK = 0;

        // Walk from large overlaps (small scroll delta) down to small overlaps
        // (big scroll delta). The middle-strip constraint means we don't need
        // to bias the search direction — a false match at maxOverlap can't
        // survive the middle-strip cross-check.
        for (int k = maxOverlap; k >= minOverlap; k--)
        {
            long error = 0;
            long budget = bestError;

            for (int s = 0; s < bStarts.Length; s++)
            {
                int bY = bStarts[s];
                for (int sy = 0; sy < stripRows; sy++)
                {
                    int aY = h - k + bY + sy;
                    if (aY < 0 || aY >= h) { error = long.MaxValue; break; }
                    for (int x = 0; x < w; x += colStep)
                    {
                        int pa = a.GetPixel(x, aY);
                        int pb = b.GetPixel(x, bY + sy);
                        error += PixelSqError(pa, pb);
                        if (error >= budget) break;
                    }
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

        double avgError = (double)bestError / samplesTotal;
        // Empirical threshold: ~900 ≈ avg per-channel diff of ~17 on each RGB.
        // Generous enough for anti-aliased text but rejects pure noise.
        if (avgError > 900) return 0;

        return bestK;
    }

    private static int PixelSqError(int a, int b)
    {
        int dr = ((a >> 16) & 0xFF) - ((b >> 16) & 0xFF);
        int dg = ((a >>  8) & 0xFF) - ((b >>  8) & 0xFF);
        int db = ( a        & 0xFF) - ( b        & 0xFF);
        return dr * dr + dg * dg + db * db;
    }

    /// <summary>
    /// Returns <c>true</c> if two same-size frames are essentially pixel-for-pixel
    /// copies of each other — i.e. nothing scrolled between them. Samples a
    /// coarse grid over the whole frame (not just a top strip like
    /// <see cref="FindBestOverlap"/>), which is the right shape for "did the
    /// page actually move?" idle detection.
    ///
    /// This separates *idle detection* from *overlap detection*. The scrolling
    /// session uses this to decide whether a frame is worth keeping, and
    /// <see cref="FindBestOverlap"/> is only used at stitch time to align the
    /// kept frames vertically.
    /// </summary>
    public static bool AreFramesNearIdentical(LockedBitmap a, LockedBitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;

        int w = a.Width;
        int h = a.Height;

        // Sample a ~64 × 32 grid over the frame. On a 1920 × 1080 capture
        // that's ~2k sample points, plenty to detect even a single-pixel
        // scroll while being fast enough to run every 500 ms.
        int xStep = Math.Max(1, w / 64);
        int yStep = Math.Max(1, h / 32);

        int differentSamples = 0;
        int totalSamples = 0;

        // Per-pixel diff threshold: any pixel whose squared RGB error exceeds
        // this is counted as "actually different" (not JPEG/anti-aliasing noise).
        // 3 * 10^2 = 300 — a per-channel diff of ~10 on each RGB.
        const int pixelDiffThreshold = 300;

        for (int y = yStep / 2; y < h; y += yStep)
        {
            for (int x = xStep / 2; x < w; x += xStep)
            {
                int pa = a.GetPixel(x, y);
                int pb = b.GetPixel(x, y);
                if (PixelSqError(pa, pb) > pixelDiffThreshold)
                {
                    differentSamples++;
                }
                totalSamples++;
            }
        }

        if (totalSamples == 0) return true;

        // If more than ~1 % of samples disagree, the frames are not identical.
        // (Fonts/cursors flicker can flip a couple of samples even on a
        // stationary frame, so we allow a tiny tolerance.)
        return differentSamples * 100 <= totalSamples;
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
