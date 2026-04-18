namespace Snapboard.Settings;

public class AppSettings
{
    // ---- Hotkeys ----
    public string CaptureHotkey { get; set; } = "PrintScreen";
    public string InstantFullScreenHotkey { get; set; } = "Ctrl+PrintScreen";
    public string ColorPickerHotkey { get; set; } = "Ctrl+Shift+C";
    public string OcrHotkey { get; set; } = "Ctrl+Shift+O";
    public string PixelRulerHotkey { get; set; } = "Ctrl+Shift+R";

    // ---- Capture behavior ----
    public bool CaptureCursor { get; set; } = false;
    public bool TrayClickCaptures { get; set; } = true;

    // ---- Lifecycle ----
    /// <summary>When true, Snapboard registers itself under HKCU\...\Run so
    /// it auto-launches (to the tray) when the user signs in.</summary>
    public bool RunOnStartup { get; set; } = false;

    // ---- Output ----
    /// <summary>"png" or "jpg"</summary>
    public string DefaultFormat { get; set; } = "png";
    public int JpegQuality { get; set; } = 92;

    /// <summary>Directory for auto-saves / instant-save. Empty = default Pictures\Snapboard.</summary>
    public string SaveDirectory { get; set; } = "";

    /// <summary>If true, edited captures skip the file dialog and are written directly to SaveDirectory.</summary>
    public bool AutoSaveAfterCapture { get; set; } = false;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
