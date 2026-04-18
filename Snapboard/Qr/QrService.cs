using System.Drawing;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace Snapboard.Qr;

/// <summary>
/// Pure-logic QR / barcode decoder. No UI, no threading, no side effects
/// — it takes a bitmap and returns either decoded payload(s) or a reason
/// why decoding failed. Mirrors <see cref="Ocr.OcrService"/> so the App
/// orchestration layer can treat both features symmetrically.
/// </summary>
public static class QrService
{
    /// <summary>One decoded code plus the format the library saw
    /// (<c>QR_CODE</c>, <c>DATA_MATRIX</c>, <c>CODE_128</c>, …).</summary>
    public readonly record struct DecodedCode(string Text, string Format);

    /// <summary>Decoder outcome. <see cref="Codes"/> is non-empty on
    /// success; on failure <see cref="Message"/> explains why. We never
    /// throw — the caller shouldn't have to wrap this in try/catch.</summary>
    public readonly record struct DecodeOutcome(
        bool Success,
        IReadOnlyList<DecodedCode> Codes,
        string? Message);

    /// <summary>
    /// Attempts to decode QR codes (and other common 1D/2D barcodes) in
    /// <paramref name="bmp"/>. Tries the full image first, then auto-scales
    /// small selections, then inverts colours — small QR images on dark
    /// backgrounds are extremely common in UIs and decode much more
    /// reliably after an inversion pass. The caller keeps ownership of
    /// <paramref name="bmp"/>.
    /// </summary>
    public static DecodeOutcome Decode(Bitmap bmp)
    {
        if (bmp == null)
            return new DecodeOutcome(false, Array.Empty<DecodedCode>(), "No bitmap was provided.");

        try
        {
            var multi = TryDecodeAll(bmp);
            if (multi.Count > 0) return new DecodeOutcome(true, multi, null);

            // Tiny selections (< 300 px on the short side) often fail
            // because QR modules end up smaller than ZXing's minimum
            // expected feature size. Upscale and retry once.
            int shortSide = Math.Min(bmp.Width, bmp.Height);
            if (shortSide > 0 && shortSide < 300)
            {
                int scale = Math.Max(2, 400 / shortSide);
                using var scaled = Upscale(bmp, scale);
                var rescaled = TryDecodeAll(scaled);
                if (rescaled.Count > 0) return new DecodeOutcome(true, rescaled, null);
            }

            // Dark-theme apps often render light-on-dark QR codes which
            // violate the "quiet-zone must be lighter than modules"
            // assumption some ZXing decoders make. One inversion pass
            // handles those without much risk of false positives.
            using var inverted = Invert(bmp);
            var invertedCodes = TryDecodeAll(inverted);
            if (invertedCodes.Count > 0) return new DecodeOutcome(true, invertedCodes, null);

            return new DecodeOutcome(false, Array.Empty<DecodedCode>(),
                "No QR code or barcode was detected in the selection. " +
                "Try selecting a tighter rectangle around the code, or zoom in first.");
        }
        catch (Exception ex)
        {
            return new DecodeOutcome(false, Array.Empty<DecodedCode>(),
                $"QR decode failed: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------

    private static IReadOnlyList<DecodedCode> TryDecodeAll(Bitmap bmp)
    {
        // TryHarder + multiple barcode formats = more robust at the cost
        // of a few extra milliseconds. We run on a background thread from
        // the caller so latency isn't user-facing.
        var reader = new BarcodeReader
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PureBarcode = false,
                PossibleFormats = new[]
                {
                    BarcodeFormat.QR_CODE,
                    BarcodeFormat.DATA_MATRIX,
                    BarcodeFormat.AZTEC,
                    BarcodeFormat.PDF_417,
                    BarcodeFormat.CODE_128,
                    BarcodeFormat.CODE_39,
                    BarcodeFormat.EAN_13,
                    BarcodeFormat.EAN_8,
                    BarcodeFormat.UPC_A,
                    BarcodeFormat.UPC_E,
                    BarcodeFormat.ITF,
                },
            },
        };

        // DecodeMultiple handles screenshots that happen to contain
        // more than one QR on a page — common on social / share screens.
        var results = reader.DecodeMultiple(bmp);
        if (results == null || results.Length == 0) return Array.Empty<DecodedCode>();

        var list = new List<DecodedCode>(results.Length);
        foreach (var r in results)
        {
            if (r == null || string.IsNullOrEmpty(r.Text)) continue;
            list.Add(new DecodedCode(r.Text, r.BarcodeFormat.ToString()));
        }
        return list;
    }

    private static Bitmap Upscale(Bitmap src, int scale)
    {
        int w = Math.Max(1, src.Width * scale);
        int h = Math.Max(1, src.Height * scale);
        var dst = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst))
        {
            g.InterpolationMode    = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode      = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.SmoothingMode        = System.Drawing.Drawing2D.SmoothingMode.None;
            g.CompositingQuality   = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.DrawImage(src, 0, 0, w, h);
        }
        return dst;
    }

    private static Bitmap Invert(Bitmap src)
    {
        // GDI+ ColorMatrix inversion — cheap, and fine for the "try the
        // opposite polarity" fallback. We don't need to preserve colour
        // accuracy since ZXing thresholds to black/white anyway.
        var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        var matrix = new System.Drawing.Imaging.ColorMatrix(new[]
        {
            new float[] { -1,  0,  0, 0, 0 },
            new float[] {  0, -1,  0, 0, 0 },
            new float[] {  0,  0, -1, 0, 0 },
            new float[] {  0,  0,  0, 1, 0 },
            new float[] {  1,  1,  1, 0, 1 },
        });
        using var attr = new System.Drawing.Imaging.ImageAttributes();
        attr.SetColorMatrix(matrix);
        g.DrawImage(src,
            new Rectangle(0, 0, src.Width, src.Height),
            0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
        return dst;
    }
}
