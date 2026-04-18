using WF = System.Windows.Forms;

namespace Snapboard.Settings;

/// <summary>
/// Parses user-visible hotkey strings like "Ctrl+Shift+A" or "PrintScreen"
/// into the (modifiers, vk) pair required by RegisterHotKey.
/// </summary>
public sealed record HotkeySpec(uint Modifiers, WF.Keys Key, string Display)
{
    public const uint MOD_ALT     = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT   = 0x4;
    public const uint MOD_WIN     = 0x8;

    public static HotkeySpec? TryParse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var parts = s.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        uint mods = 0;
        WF.Keys key = WF.Keys.None;

        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= MOD_CONTROL; break;
                case "shift":
                    mods |= MOD_SHIFT; break;
                case "alt":
                    mods |= MOD_ALT; break;
                case "win":
                case "windows":
                    mods |= MOD_WIN; break;
                default:
                    if (!Enum.TryParse<WF.Keys>(p, ignoreCase: true, out var k))
                        return null;
                    key = k;
                    break;
            }
        }

        if (key == WF.Keys.None) return null;
        return new HotkeySpec(mods, key, Format(mods, key));
    }

    public static string Format(uint mods, WF.Keys key)
    {
        var parts = new List<string>();
        if ((mods & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & MOD_ALT)     != 0) parts.Add("Alt");
        if ((mods & MOD_SHIFT)   != 0) parts.Add("Shift");
        if ((mods & MOD_WIN)     != 0) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
