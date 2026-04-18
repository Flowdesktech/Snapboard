using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snapboard.Updates;

/// <summary>
/// Metadata about a single GitHub release relevant to auto-update — the
/// parsed version, release notes, and the URL of the installer asset we
/// should download to perform the upgrade.
/// </summary>
public sealed record UpdateInfo(
    Version Version,
    string TagName,
    string ReleaseNotes,
    string HtmlUrl,
    string InstallerUrl,
    long InstallerSize);

/// <summary>
/// Talks to the GitHub Releases API to discover and download a newer
/// Snapboard build. No authentication is used — the public API is called
/// with a descriptive <c>User-Agent</c> and the standard 60-request/hour
/// rate limit, which is more than enough for one daily check.
///
/// Intentionally minimal: one method to check, one to download, one to
/// launch the installer. All of them run on a background thread and never
/// throw — failures are surfaced as <c>null</c> / <c>false</c> so the
/// caller can degrade gracefully.
/// </summary>
public static class UpdateService
{
    private const string Owner = "Flowdesktech";
    private const string Repo  = "Snapboard";

    // Assets we prefer, in order. The installer is the best upgrade path
    // because it migrates the previous install; the standalone exe is a
    // fallback we don't currently auto-apply.
    private static readonly string[] InstallerAssetSuffixes = { "-Setup.exe" };

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Snapboard-Updater/1.0 (+https://github.com/Flowdesktech/Snapboard)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>
    /// Returns the currently-running Snapboard version as declared in the
    /// assembly's <c>InformationalVersion</c> (falls back to the file
    /// version). This is the version we compare against GitHub.
    /// </summary>
    public static Version GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info) && TryParseSemVer(info, out var v)) return v;

        var name = asm.GetName().Version;
        return name ?? new Version(0, 0, 0);
    }

    /// <summary>
    /// Checks the public "latest release" endpoint and returns metadata if
    /// a release is found. Returns <c>null</c> on any failure (network
    /// error, rate limit, malformed payload) — callers should treat that
    /// as "no update available".
    /// </summary>
    public static async Task<UpdateInfo?> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, SerializerOptions, ct)
                .ConfigureAwait(false);
            if (payload == null || payload.Draft || payload.Prerelease) return null;
            if (string.IsNullOrWhiteSpace(payload.TagName)) return null;

            if (!TryParseSemVer(payload.TagName, out var version)) return null;

            var installer = PickInstaller(payload.Assets);
            if (installer == null) return null;

            return new UpdateInfo(
                Version: version,
                TagName: payload.TagName,
                ReleaseNotes: payload.Body ?? string.Empty,
                HtmlUrl: payload.HtmlUrl ?? string.Empty,
                InstallerUrl: installer.BrowserDownloadUrl ?? string.Empty,
                InstallerSize: installer.Size);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer referenced by <paramref name="info"/> to a
    /// temp file and returns the local path. Reports progress (0..1) as a
    /// fraction of total bytes when Content-Length is known.
    /// </summary>
    public static async Task<string?> DownloadInstallerAsync(
        UpdateInfo info,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Snapboard-Updates");
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, $"Snapboard-{info.TagName.TrimStart('v')}-Setup.exe");

            using var req = new HttpRequestMessage(HttpMethod.Get, info.InstallerUrl);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            long total = resp.Content.Headers.ContentLength ?? info.InstallerSize;
            await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var dst = File.Create(filePath);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total > 0 && progress != null)
                {
                    progress.Report(Math.Clamp((double)read / total, 0, 1));
                }
            }

            if (total > 0 && progress != null) progress.Report(1);
            return filePath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Launches the downloaded installer and returns <c>true</c> if the
    /// process was started successfully. The installer is run silently so
    /// the upgrade feels seamless; caller is expected to exit the app
    /// immediately after so the installer can replace binaries in place.
    /// </summary>
    public static bool LaunchInstaller(string installerPath)
    {
        try
        {
            if (!File.Exists(installerPath)) return false;

            // Inno Setup silent flags:
            //   /SILENT          → minimal progress dialog, no wizard
            //   /SUPPRESSMSGBOXES → skip recoverable prompts
            //   /NORESTART       → never reboot; we don't need it
            //   /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS → gracefully close
            //     and restart Snapboard if we somehow haven't exited yet
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? Environment.CurrentDirectory,
            };
            var proc = System.Diagnostics.Process.Start(psi);
            return proc != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a SemVer-ish string, tolerating a leading 'v' and
    /// pre-release/metadata suffixes (which we strip — a "-rc1" release is
    /// treated as its base version for comparison purposes, and
    /// pre-releases are filtered out earlier anyway).
    /// </summary>
    public static bool TryParseSemVer(string s, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(s)) return false;

        var trimmed = s.Trim().TrimStart('v', 'V').Split(new[] { '-', '+' }, 2)[0];
        return Version.TryParse(trimmed, out version!);
    }

    private static GitHubAsset? PickInstaller(IReadOnlyList<GitHubAsset>? assets)
    {
        if (assets == null || assets.Count == 0) return null;
        foreach (var suffix in InstallerAssetSuffixes)
        {
            foreach (var a in assets)
            {
                if (!string.IsNullOrEmpty(a.Name) &&
                    a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
                {
                    return a;
                }
            }
        }
        return null;
    }

    // ------------------------------------------------------------------
    // GitHub API payload
    // ------------------------------------------------------------------
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]   public string? TagName { get; set; }
        [JsonPropertyName("name")]       public string? Name { get; set; }
        [JsonPropertyName("body")]       public string? Body { get; set; }
        [JsonPropertyName("html_url")]   public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")]      public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")]     public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]                 public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")]                 public long Size { get; set; }
    }
}
