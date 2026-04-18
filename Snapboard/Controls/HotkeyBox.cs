using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WF = System.Windows.Forms;

namespace Snapboard.Controls;

/// <summary>
/// Read-only text box that records a keyboard shortcut when focused.
/// The current hotkey is exposed as the <see cref="Hotkey"/> dependency property
/// (string in "Ctrl+Shift+A" / "PrintScreen" form — parseable by HotkeySpec).
/// </summary>
public class HotkeyBox : TextBox
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHotkeyChanged));

    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        Text = "Not set";
        PreviewKeyDown += OnPreviewKeyDown;
        GotKeyboardFocus += (_, _) => Text = "Press a key combination…";
        LostKeyboardFocus += (_, _) => Text = Display(Hotkey);
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyBox)d;
        if (!box.IsKeyboardFocused)
        {
            box.Text = Display(e.NewValue as string ?? string.Empty);
        }
    }

    private static string Display(string s) => string.IsNullOrWhiteSpace(s) ? "Not set" : s;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Swallow modifier-only presses; wait for the "real" key.
        if (key is Key.LeftCtrl or Key.RightCtrl
                or Key.LeftShift or Key.RightShift
                or Key.LeftAlt  or Key.RightAlt
                or Key.LWin     or Key.RWin)
        {
            return;
        }

        if (key == Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        if (key == Key.Back || key == Key.Delete)
        {
            Hotkey = string.Empty;
            Text = "Not set";
            return;
        }

        var mods = Keyboard.Modifiers;
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control))  parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt))      parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift))    parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows))  parts.Add("Win");

        int vk = KeyInterop.VirtualKeyFromKey(key);
        var wfKey = (WF.Keys)vk;
        parts.Add(wfKey.ToString());

        Hotkey = string.Join("+", parts);
        Text = Hotkey;
    }
}
