using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipYourself.Desktop.Interop;

/// <summary>Raises an event whenever the Windows clipboard changes (WM_CLIPBOARDUPDATE).</summary>
public sealed class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private readonly HwndSource _source;
    private bool _disposed;

    public event Action? ClipboardChanged;

    public ClipboardMonitor(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.EnsureHandle())
                  ?? throw new InvalidOperationException("Window handle unavailable.");
        _source.AddHook(WndProc);
        AddClipboardFormatListener(_source.Handle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            ClipboardChanged?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
