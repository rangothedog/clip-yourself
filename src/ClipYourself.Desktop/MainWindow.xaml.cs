using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ClipYourself.Core.Models;
using ClipYourself.Desktop.ViewModels;

namespace ClipYourself.Desktop;

public partial class MainWindow : Window
{
    private const string ClipDragFormat = MainViewModel.ClipDragFormat;

    private Point _dragStart;
    private ClipItem? _dragClip;
    private DispatcherTimer? _dropOverlayHideTimer;
    private Interop.AppBarService? _appBar;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += (_, _) => _appBar?.Refresh();

        // A borderless window that gets maximized (Win+Up / snap) covers the whole
        // screen with no way back — undo it immediately and re-dock instead.
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                if (_appBar?.IsDocked == true) _appBar.Refresh();
                else DockRight();
            }
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SquareOffCorners();
        _appBar = new Interop.AppBarService(this);
        if (ViewModel is { } vm)
        {
            vm.SetDockMode = SetDockMode;
            if (vm.ReserveDesktopSpace) { _appBar.Dock(); return; }
        }
        DockRight();
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DONOTROUND = 1;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>
    /// Windows 11 rounds top-level window corners, which leaves wedges of desktop
    /// showing where a docked sidebar meets the screen corners. Square them off so
    /// the edges seam flush against the screen and taskbar.
    /// </summary>
    private void SquareOffCorners()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        int pref = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    private void SetDockMode(bool reserve)
    {
        if (_appBar == null) return;
        if (reserve) _appBar.Dock();
        else { _appBar.Undock(); DockRight(); }
    }

    /// <summary>Release the reserved desktop space. Call before the app exits.</summary>
    public void ReleaseAppBar() => _appBar?.Undock();

    /// <summary>Snap the sidebar to the right edge of the work area.</summary>
    private void DockRight()
    {
        var workArea = SystemParameters.WorkArea;
        Height = workArea.Height;
        Top = workArea.Top;
        Left = workArea.Right - Width;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Closing hides the sidebar; the app keeps running so Ctrl+Alt+V can summon it.
        if (!App.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // A docked AppBar is pinned to the edge — dragging it would fight the shell.
        if (_appBar?.IsDocked == true) return;
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true; // the box never types; it only captures combos
        var gesture = Interop.HotkeyGesture.FromKeyEvent(e);
        if (gesture != null && ViewModel is { } vm) vm.HotkeyGesture = gesture;
    }

    // ----- dragging a clip card onto a drawer -----

    private void ClipCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragClip = (sender as FrameworkElement)?.DataContext as ClipItem;
    }

    private void ClipCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragClip == null || e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var clip = _dragClip;
        _dragClip = null;
        if (ViewModel is not { } vm) return;

        // Move is for internal reorder/file; Copy is for dropping out to Explorer
        // (copy-only there, so a shell drop can never relocate a blob or original).
        DragDrop.DoDragDrop((DependencyObject)sender, vm.BuildDragData(clip),
            DragDropEffects.Copy | DragDropEffects.Move);
    }

    // ----- dropping a clip onto another clip reorders it -----

    private void ClipCard_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ClipDragFormat)) return; // let files/media bubble up
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ClipCard_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Data.GetData(ClipDragFormat) is not string clipId) return;
        if ((sender as FrameworkElement)?.DataContext is not ClipItem target) return;
        vm.ReorderClip(clipId, target);
        e.Handled = true;
    }

    private void DrawerRow_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = AcceptsDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DrawerRow_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if ((sender as FrameworkElement)?.DataContext is not Drawer drawer) return;
        HandleExternalDrop(e, drawer);
        e.Handled = true;
    }

    private void SessionBar_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = AcceptsDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void SessionBar_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        HandleExternalDrop(e, vm.SessionDrawer);
        e.Handled = true;
    }

    // ----- dropping external content anywhere on the sidebar -----

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ClipDragFormat)) return; // internal drag that missed a drawer
        HandleExternalDrop(e, null);
    }

    /// <summary>Routes a drop to the right capture path. Order matters: files, then media URL, then text.</summary>
    private void HandleExternalDrop(DragEventArgs e, Drawer? target)
    {
        if (ViewModel is not { } vm) return;

        if (e.Data.GetData(ClipDragFormat) is string clipId)
        {
            vm.MoveClipToDrawer(clipId, target ?? vm.SessionDrawer);
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            vm.AddDroppedFiles(files, target);
        }
        else if (TryGetMediaUrl(e.Data, out var url, out var name))
        {
            // Browser image/video drag: fetch the bytes rather than storing the link.
            vm.FetchAndAddMedia(url, name, target);
        }
        else if (e.Data.GetData(DataFormats.UnicodeText) is string text)
        {
            vm.AddDroppedText(text, target);
        }
    }

    private static bool AcceptsDrop(IDataObject data)
        => data.GetDataPresent(ClipDragFormat)
           || data.GetDataPresent(DataFormats.FileDrop)
           || data.GetDataPresent("DownloadURL")
           || data.GetDataPresent(DataFormats.UnicodeText)
           || data.GetDataPresent("text/x-moz-url");

    /// <summary>
    /// Extracts a downloadable media URL from a browser drag. Chrome/Edge expose
    /// "DownloadURL" (mime:filename:url); Firefox uses "text/x-moz-url"; otherwise
    /// a plain URL that ends in a known media extension counts.
    /// </summary>
    private static bool TryGetMediaUrl(IDataObject data, out string url, out string? fileName)
    {
        url = "";
        fileName = null;

        if (data.GetDataPresent("DownloadURL"))
        {
            var raw = ReadUnicode(data.GetData("DownloadURL"));
            var parts = raw.Split(':', 3); // mime : filename : url(with colons)
            if (parts.Length == 3 && parts[2].StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                fileName = string.IsNullOrWhiteSpace(parts[1]) ? null : parts[1];
                url = parts[2].Trim();
                return true;
            }
        }

        if (data.GetDataPresent("text/x-moz-url"))
        {
            var raw = ReadUnicode(data.GetData("text/x-moz-url"));
            var first = raw.Split('\n', '\r').FirstOrDefault(s => s.StartsWith("http", StringComparison.OrdinalIgnoreCase));
            if (first != null && LooksLikeMedia(first))
            {
                url = first.Trim();
                return true;
            }
        }

        if (data.GetData(DataFormats.UnicodeText) is string text)
        {
            var candidate = text.Trim();
            if (candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase) && LooksLikeMedia(candidate))
            {
                url = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeMedia(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        return Services.ClipCapture.IsImageFile(path) || Services.ClipCapture.IsVideoFile(path);
    }

    private static string ReadUnicode(object? data) => data switch
    {
        string s => s,
        MemoryStream ms => System.Text.Encoding.Unicode.GetString(ms.ToArray()).TrimEnd('\0'),
        byte[] bytes => System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0'),
        _ => ""
    };

    // ----- "drop to clip" overlay while external content hovers the sidebar -----

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = AcceptsDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ClipDragFormat)) return; // internal filing drag
        if (!AcceptsDrop(e.Data)) return;

        _dropOverlayHideTimer?.Stop();
        DropOverlay.Visibility = Visibility.Visible;
    }

    private void Window_PreviewDragLeave(object sender, DragEventArgs e)
    {
        // DragLeave also fires between child elements; debounce so the overlay
        // only hides when the cursor actually left the window.
        _dropOverlayHideTimer ??= CreateOverlayHideTimer();
        _dropOverlayHideTimer.Stop();
        _dropOverlayHideTimer.Start();
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        _dropOverlayHideTimer?.Stop();
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private DispatcherTimer CreateOverlayHideTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            DropOverlay.Visibility = Visibility.Collapsed;
        };
        return timer;
    }
}
