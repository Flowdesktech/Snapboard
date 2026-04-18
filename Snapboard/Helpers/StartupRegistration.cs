using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Snapboard.Helpers;

/// <summary>
/// Manages the per-user "run at logon" entry under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>. Per-user means
/// no UAC prompt is required, and the setting is scoped to the signed-in
/// account (which is what a tray app like Snapboard wants).
///
/// The auto-launched process is started with a <c>--autostart</c> flag so the
/// app can skip showing the dashboard window and boot directly into the tray.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName  = "Snapboard";
    public  const string AutoStartArg = "--autostart";

    /// <summary>Write or remove the Run key entry based on <paramref name="enabled"/>.
    /// Swallows registry exceptions so a settings save can't crash the app —
    /// worst case the user just doesn't get auto-start.</summary>
    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                key.SetValue(ValueName, BuildCommandLine(), RegistryValueKind.String);
            }
            else
            {
                // DeleteValue with throwOnMissingValue: false is idempotent.
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StartupRegistration.Apply failed: {ex.Message}");
        }
    }

    /// <summary>True if the Run key currently has a Snapboard entry (regardless
    /// of whether it points at the current exe). Used to sync the settings
    /// checkbox on startup so the UI matches reality.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Current exe path, quoted, plus the <c>--autostart</c> flag.
    /// Quoted so spaces in "C:\Program Files\…" don't break the command.</summary>
    private static string BuildCommandLine()
    {
        string exe = GetExecutablePath();
        return $"\"{exe}\" {AutoStartArg}";
    }

    private static string GetExecutablePath()
    {
        // Environment.ProcessPath is the actual .exe (not the .dll), which
        // matters for self-contained WPF apps where the exe is a native host.
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return path;

        // Fall back to the module filename of the current process.
        return Process.GetCurrentProcess().MainModule?.FileName
               ?? AppContext.BaseDirectory;
    }
}
