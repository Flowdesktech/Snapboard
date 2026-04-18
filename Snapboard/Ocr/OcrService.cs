using System.IO;
using System.Threading;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using SD = System.Drawing;

namespace Snapboard.Ocr;

/// <summary>
/// Wraps <see cref="OcrEngine"/> for easy consumption from a WPF app.
/// Uses whichever OCR language packs the user has installed in Windows
/// (Settings → Time &amp; Language → Language → Add a language).
///
/// Every step is pushed off the UI thread so a slow or stuck OCR call can
/// never freeze the app that invokes this service.
/// </summary>
public static class OcrService
{
    public readonly record struct OcrOutcome(bool Success, string Text, string? LanguageTag, string? Message)
    {
        public static OcrOutcome Ok(string text, string? lang)  => new(true,  text, lang, null);
        public static OcrOutcome Fail(string message)            => new(false, string.Empty, null, message);
    }

    public static async Task<OcrOutcome> RecognizeAsync(
        SD.Bitmap bitmap,
        string? preferredLanguageTag = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1) CPU-bound: PNG-encode the bitmap on the thread pool.
            byte[] pngBytes = await Task.Run(() =>
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms, SD.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // 2) WinRT decode → SoftwareBitmap.
            var ras = new InMemoryRandomAccessStream();
            using (var outputStream = ras.GetOutputStreamAt(0))
            using (var writer = new DataWriter(outputStream))
            {
                writer.WriteBytes(pngBytes);
                await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
                writer.DetachStream();
            }
            ras.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(ras).AsTask(cancellationToken).ConfigureAwait(false);
            using var softwareBitmap = await decoder
                .GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            // 3) Pick an OCR engine.
            OcrEngine? engine = null;
            string? lang = null;

            if (!string.IsNullOrWhiteSpace(preferredLanguageTag))
            {
                var tagged = new Language(preferredLanguageTag);
                if (OcrEngine.IsLanguageSupported(tagged))
                {
                    engine = OcrEngine.TryCreateFromLanguage(tagged);
                    lang = tagged.LanguageTag;
                }
            }

            if (engine == null)
            {
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
                lang = engine?.RecognizerLanguage?.LanguageTag;
            }

            if (engine == null)
            {
                var supported = OcrEngine.AvailableRecognizerLanguages;
                if (supported != null && supported.Count > 0)
                {
                    engine = OcrEngine.TryCreateFromLanguage(supported[0]);
                    lang = supported[0].LanguageTag;
                }
            }

            if (engine == null)
            {
                return OcrOutcome.Fail(
                    "No OCR language packs are installed on this machine.\n\n" +
                    "Open Windows Settings → Time & Language → Language, add a language " +
                    "(e.g. English – United States), and make sure its optional OCR / " +
                    "handwriting feature is installed. Then try again.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 4) Recognize.
            var result = await engine
                .RecognizeAsync(softwareBitmap)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);

            return OcrOutcome.Ok(result.Text ?? string.Empty, lang);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OcrOutcome.Fail($"OCR failed: {ex.Message}");
        }
    }
}
