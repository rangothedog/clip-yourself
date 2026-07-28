using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipYourself.Desktop.Interop;

/// <summary>Registers the configurable global hotkey that summons the sidebar.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xC11F;

    private readonly HwndSource _source;
    private readonly Action _callback;
    private bool _disposed;

    public bool Registered { get; private set; }

    public HotkeyService(Window window, string gesture, Action callback)
    {
        _callback = callback;
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.EnsureHandle())
                  ?? throw new InvalidOperationException("Window handle unavailable.");
        _source.AddHook(WndProc);
        Registered = Register(gesture);
    }

    /// <summary>Swaps the registered combo; returns false (and leaves nothing registered) on failure.</summary>
    public bool TryRebind(string gesture)
    {
        if (Registered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            Registered = false;
        }
        Registered = Register(gesture);
        return Registered;
    }

    private bool Register(string gesture)
    {
        if (!HotkeyGesture.TryParse(gesture, out var modifiers, out var vk)) return false;
        return RegisterHotKey(_source.Handle, HotkeyId, modifiers | HotkeyGesture.MOD_NOREPEAT, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _callback();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Registered) UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
