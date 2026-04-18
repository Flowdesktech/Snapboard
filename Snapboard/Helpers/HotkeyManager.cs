using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Snapboard.Helpers;

/// <summary>
/// Registers Win32 global hotkeys via a hidden message-only NativeWindow.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    private readonly MessageWindow _window;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId;

    public HotkeyManager()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
    }

    public void Register(Keys key, Action onPressed, uint modifiers = 0)
    {
        int id = System.Threading.Interlocked.Increment(ref _nextId);
        if (!RegisterHotKey(_window.Handle, id, modifiers, (uint)key))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to register hotkey {key}.");
        }
        _actions[id] = onPressed;
    }

    /// <summary>
    /// Attempts to register a hotkey; returns false on failure (e.g. already owned
    /// by another process) instead of throwing.
    /// </summary>
    public bool TryRegister(Keys key, Action onPressed, uint modifiers = 0)
    {
        try { Register(key, onPressed, modifiers); return true; }
        catch { return false; }
    }

    /// <summary>Unregisters every hotkey previously registered through this manager.</summary>
    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_window.Handle, id);
        }
        _actions.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly HotkeyManager _owner;
        public MessageWindow(HotkeyManager owner) { _owner = owner; }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && _owner._actions.TryGetValue(m.WParam.ToInt32(), out var action))
            {
                try { action(); } catch { /* keep message loop alive */ }
            }
            base.WndProc(ref m);
        }
    }
}
