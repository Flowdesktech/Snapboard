using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Snapboard.Helpers;

/// <summary>
/// Sends a captured bitmap to Google or Bing's reverse image search and opens
/// the results in the user's default browser.
///
/// We upload the raw image bytes to the search engine directly (no third-party
/// image host), keeping the "privacy-first, no upload to our servers" promise
/// while still delivering the feature.
///
/// If the upload fails for any reason (network, endpoint changed, rate limits),
/// we fall back to copying the image to the clipboard and opening the search
/// engine's visual-search landing page so the user can Ctrl+V to continue.
/// </summary>
public static class ReverseImageSearch
{
    // Stable modern UA. Some engines return 400 for non-browser UAs.
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private const string GoogleUploadUrl  = "https://www.google.com/searchbyimage/upload";
    private const string GoogleFallback   = "https://images.google.com/";
    private const string BingUploadUrl    = "https://www.bing.com/images/search?view=detailv2&iss=sbiupload&FORM=ANCMS1&sbisrc=UrlPaste";
    private const string BingFallback     = "https://www.bing.com/visualsearch";

    public enum Engine { Google, Bing }

    public static async Task<bool> SearchAsync(Engine engine, BitmapSource image, CancellationToken ct = default)
    {
        byte[] png = EncodePng(image);

        try
        {
            string? url = engine switch
            {
                Engine.Google => await UploadToGoogleAsync(png, ct),
                Engine.Bing   => await UploadToBingAsync(png, ct),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                OpenUrl(url);
                return true;
            }
        }
        catch
        {
            // fall through to fallback
        }

        // Fallback path: copy to clipboard, open landing page, nudge the user.
        try
        {
            CopyImageToClipboardSafe(image);
            string fallback = engine == Engine.Google ? GoogleFallback : BingFallback;
            OpenUrl(fallback);

            MessageBox.Show(
                engine == Engine.Google
                    ? "Google couldn't accept the upload automatically.\n\nThe image has been copied to your clipboard — paste it (Ctrl+V) into the Google Images / Lens search box."
                    : "Bing couldn't accept the upload automatically.\n\nThe image has been copied to your clipboard — paste it (Ctrl+V) into the Bing Visual Search page.",
                "Snapboard — reverse image search",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- Google

    private static async Task<string?> UploadToGoogleAsync(byte[] png, CancellationToken ct)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        // Google's endpoint expects the file under "encoded_image".
        content.Add(file, "encoded_image", "snapboard.png");
        content.Add(new StringContent(""), "image_url");
        content.Add(new StringContent("cr_1_5_2"), "sbisrc");

        using var resp = await client.PostAsync(GoogleUploadUrl, content, ct);

        // Happy path: 302 Found / 303 See Other with a Location header to the
        // results page (usually on lens.google.com).
        if ((int)resp.StatusCode is >= 300 and < 400)
        {
            var loc = resp.Headers.Location;
            if (loc != null)
            {
                string target = loc.IsAbsoluteUri
                    ? loc.ToString()
                    : new Uri(new Uri(GoogleUploadUrl), loc).ToString();
                return target;
            }
        }

        // Some variants return 200 with the result URL embedded in the page.
        if (resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(ct);
            var m = Regex.Match(body, @"https?://lens\.google\.com/[^\s""'<>]+");
            if (m.Success) return m.Value;
            m = Regex.Match(body, @"https?://www\.google\.com/search\?[^""'<>]*tbs=sbi[^""'<>]*");
            if (m.Success) return m.Value;
        }

        return null;
    }

    // ------------------------------------------------------------------ Bing

    private static async Task<string?> UploadToBingAsync(byte[] png, CancellationToken ct)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");

        // Bing's visual-search endpoint accepts a base64-encoded image in the
        // "imageBin" field.
        string b64 = Convert.ToBase64String(png);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(b64), "imageBin" }
        };

        using var resp = await client.PostAsync(BingUploadUrl, content, ct);
        if (resp.IsSuccessStatusCode)
        {
            // If the redirect landed us on a real result page, use that URL.
            var final = resp.RequestMessage?.RequestUri?.ToString();
            if (!string.IsNullOrWhiteSpace(final) &&
                (final!.Contains("bing.com/images/search", StringComparison.OrdinalIgnoreCase) ||
                 final.Contains("bing.com/images/detail",  StringComparison.OrdinalIgnoreCase)))
            {
                return final;
            }

            // Otherwise try to extract an insightsToken / result URL out of the page.
            string body = await resp.Content.ReadAsStringAsync(ct);
            var token = Regex.Match(body, @"""insightsToken""\s*:\s*""([^""]+)""");
            if (token.Success)
            {
                return "https://www.bing.com/images/search?view=detailv2&insightsToken="
                       + Uri.EscapeDataString(token.Groups[1].Value);
            }
        }

        return null;
    }

    // ------------------------------------------------------------ Utilities

    private static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void CopyImageToClipboardSafe(BitmapSource image)
    {
        try { Clipboard.SetImage(image); } catch { /* clipboard can be locked */ }
    }
}
