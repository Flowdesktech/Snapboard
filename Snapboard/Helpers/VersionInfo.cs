using System.Reflection;

namespace Snapboard.Helpers;

/// <summary>
/// Single source of truth for the app's display version, loaded from the
/// assembly's <see cref="AssemblyInformationalVersionAttribute"/>. MSBuild
/// sets that attribute from the <c>&lt;Version&gt;</c> property in the .csproj
/// (which CI can override via <c>-p:Version=x.y.z</c> at release time), so
/// the UI, the About dialog, and any other place that wants to display a
/// version number all read the exact same string.
/// </summary>
public static class VersionInfo
{
    private static readonly Lazy<string> _display = new(ResolveDisplayVersion);

    /// <summary>The version as it should appear in UI, e.g. "0.1.0".
    /// Strips the "+commit-hash" SourceLink suffix if present.</summary>
    public static string Display => _display.Value;

    /// <summary>Convenience: the version prefixed with "v", e.g. "v0.1.0".</summary>
    public static string DisplayWithV => "v" + Display;

    private static string ResolveDisplayVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();

            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                // SourceLink appends "+<commit-hash>" — we only want the SemVer core.
                int plus = info!.IndexOf('+');
                return plus > 0 ? info[..plus] : info;
            }

            if (asm.GetName().Version is { } v)
                return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch { /* fall through to hardcoded fallback */ }

        return "0.0.0";
    }
}
