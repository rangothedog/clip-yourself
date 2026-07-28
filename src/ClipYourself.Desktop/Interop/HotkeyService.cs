using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipYourself.Desktop.Interop;

/// <summary>Registers the global Ctrl+Alt+V hotkey that summons the sidebar.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xC11F;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_V = 0x56;

    private readonly HwndSource _source;
    private readonly Action _callback;
    private bool _disposed;

    public bool Registered { get; }

    public HotkeyService(Window window, Action callback)
    {
        _callback = callback;
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.EnsureHandle())
                  ?? throw new InvalidOperationException("Window handle unavailable.");
        _source.AddHook(WndProc);
        Registered = RegisterHotKey(_source.Handle, HotkeyId, MOD_CONTROL | MOD_ALT, VK_V);
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
