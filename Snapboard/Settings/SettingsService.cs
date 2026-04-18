using System.IO;
using System.Text.Json;

namespace Snapboard.Settings;

public static class SettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Snapboard", "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Ignore corrupted files; fall through to defaults.
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Swallow IO errors — settings are non-critical state.
        }
    }

    /// <summary>Returns the effective directory for auto-saves / instant-save.</summary>
    public static string ResolveSaveDirectory(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SaveDirectory))
            return settings.SaveDirectory;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Snapboard");
    }
}
