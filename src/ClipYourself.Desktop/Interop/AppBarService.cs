using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipYourself.Desktop.Interop;

/// <summary>
/// Registers the window as a Windows AppBar docked to the right edge, so it
/// reserves desktop work area — other apps (including maximized ones) tile
/// beside it instead of underneath it.
/// </summary>
public sealed class AppBarService
{
    private const int ABM_NEW = 0;
    private const int ABM_REMOVE = 1;
    private const int ABM_QUERYPOS = 2;
    private const int ABM_SETPOS = 3;
    private const int ABE_RIGHT = 2;
    private const int ABN_POSCHANGED = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private readonly Window _window;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private uint _callbackId;
    private bool _registered;

    public bool IsDocked => _registered;

    public AppBarService(Window window) => _window = window;

    public void Dock()
    {
        var helper = new WindowInteropHelper(_window);
        _hwnd = helper.EnsureHandle();

        if (!_registered)
        {
            _callbackId = RegisterWindowMessage("ClipYourself.AppBar." + _hwnd);
            var data = NewData();
            data.uCallbackMessage = _callbackId;
            SHAppBarMessage(ABM_NEW, ref data);

            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(WndProc);
            _registered = true;
            _window.Topmost = false;
        }
        SetPosition();
    }

    public void Undock()
    {
        if (!_registered) return;
        var data = NewData();
        SHAppBarMessage(ABM_REMOVE, ref data);
        _source?.RemoveHook(WndProc);
        _registered = false;
    }

    /// <summary>Re-reserves space (call after the width changes).</summary>
    public void Refresh()
    {
        if (_registered) SetPosition();
    }

    private void SetPosition()
    {
        var scale = _source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var widthPx = (int)Math.Round(_window.Width * scale);

        var monitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        var mon = info.rcMonitor;

        var data = NewData();
        data.uEdge = ABE_RIGHT;
        data.rc = new RECT { left = mon.right - widthPx, top = mon.top, right = mon.right, bottom = mon.bottom };

        // Let the shell adjust for other appbars, then pin our width to the right edge.
        SHAppBarMessage(ABM_QUERYPOS, ref data);
        data.rc.left = data.rc.right - widthPx;
        SHAppBarMessage(ABM_SETPOS, ref data);

        var r = data.rc;
        MoveWindow(_hwnd, r.left, r.top, r.right - r.left, r.bottom - r.top, true);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)_callbackId && wParam.ToInt32() == ABN_POSCHANGED)
        {
            SetPosition();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private APPBARDATA NewData() => new()
    {
        cbSize = Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
