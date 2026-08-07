using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClipYourself.Core.Models;
using ClipYourself.Desktop.ViewModels;

namespace ClipYourself.Desktop;

public partial class MainWindow : Window
{
    private const string ClipDragFormat = MainViewModel.ClipDragFormat;
    private const string DrawerDragFormat = MainViewModel.DrawerDragFormat;

    private Point _dragStart;
    private ClipItem? _dragClip;
    private Point _drawerDragStart;
    private Drawer? _dragDrawer;
    private DispatcherTimer? _dropOverlayHideTimer;
    private Interop.AppBarService? _appBar;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += Window_PreviewKeyDown;
        SizeChanged += (_, _) => _appBar?.Refresh();
        IsVisibleChanged += OnIsVisibleChanged;

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
            else if (WindowState == WindowState.Minimized)
            {
                // Give the reserved space back while minimized.
                _appBar?.Undock();
            }
            else if (ViewModel is { ReserveDesktopSpace: true })
            {
                // Restored from minimized — reclaim the dock.
                _appBar?.Dock();
            }
        };
    }

    /// <summary>
    /// The AppBar reservation lives until it's explicitly removed, so hiding the
    /// sidebar (— button, Ctrl+Alt+V, close) would otherwise keep its desktop space
    /// reserved. Release it when hidden and reclaim it when shown.
    /// </summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_appBar == null) return;
        if (ViewModel is not { ReserveDesktopSpace: true }) return;
        if ((bool)e.NewValue) _appBar.Dock();
        else _appBar.Undock();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SquareOffCorners();
        _appBar = new Interop.AppBarService(this);
        if (ViewModel is { } vm)
        {
            vm.SetDockMode = SetDockMode;
            vm.SetSidebarWidth = ApplyWidth;
            vm.BeginRenameDrawer = BeginRenameDrawer;
            Width = vm.SidebarWidth;
            if (vm.ReserveDesktopSpace) { _appBar.Dock(); return; }
        }
        DockRight();
    }

    private void ApplyWidth(double width)
    {
        Width = width;
        // Height/edges are pinned; just re-seat at the new width.
        if (_appBar?.IsDocked == true) _appBar.Refresh();
        else DockRight();
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
        if (reserve) { _appBar.Dock(); return; }

        // Releasing the AppBar reservation doesn't update SystemParameters.WorkArea
        // synchronously — reposition after the shell has reclaimed the space, or we'd
        // read the still-shrunk work area and land the window mid-screen.
        _appBar.Undock();
        Dispatcher.BeginInvoke(new Action(DockRight), DispatcherPriority.Background);
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

    /// <summary>
    /// A new drawer just opened in the reel view. The reel is shown via a binding, so
    /// its name box isn't laid out yet — wait for that pass, then select the whole name
    /// so the user can rename it by just typing, without reaching for the mouse.
    /// </summary>
    private void BeginRenameDrawer()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ReelNameBox.Focus();
            ReelNameBox.SelectAll();
        }), DispatcherPriority.Background);
    }

    /// <summary>Ctrl+V pastes into the current drawer; Delete removes selected clips — unless a text field is focused.</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // A focused text field (rename box, search, hotkey capture) owns these keys.
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;

        if (e.Key == Key.Delete)
        {
            if (ViewModel is { HasSelectedClips: true } vm)
            {
                vm.DeleteSelectedClips();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ViewModel?.PasteIntoCurrentDrawer();
            e.Handled = true;
        }
    }

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
        if (ViewModel is not { } vm || _dragClip is not { } clip) return;

        // Shift/Ctrl are selection gestures — handle them here and mark the event handled
        // so the card's click-to-copy Command doesn't also fire. A plain press just sets
        // the anchor; the copy (and selection reset) happens on the click that follows.
        var mods = Keyboard.Modifiers;
        if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            vm.RangeSelectTo(clip);
            e.Handled = true;
        }
        else if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
        {
            vm.ToggleClipSelection(clip);
            e.Handled = true;
        }
        else
        {
            vm.SetSelectionAnchor(clip);
        }
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

        // Grabbing a selected clip drags the whole selection; grabbing an unselected one drags just it.
        var clips = clip.IsSelected ? vm.SelectedClipsInOrder() : new List<ClipItem> { clip };
        if (clips.Count == 0) clips = new List<ClipItem> { clip };

        // Move is for internal reorder/file; Copy is for dropping out to Explorer
        // (copy-only there, so a shell drop can never relocate a blob or original).
        DragDrop.DoDragDrop((DependencyObject)sender, vm.BuildDragData(clips),
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
        if (e.Data.GetData(ClipDragFormat) is not string idBlob) return;
        if ((sender as FrameworkElement)?.DataContext is not ClipItem target) return;
        vm.ReorderClips(idBlob.Split('\n', StringSplitOptions.RemoveEmptyEntries), target);
        e.Handled = true;
    }

    // ----- dragging a drawer row onto another reorders it -----

    private void DrawerRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // A press that starts on the 🗑 (or any nested button) must not begin a reorder —
        // let it click through to delete instead.
        if (PressedOnNestedButton(e.OriginalSource as DependencyObject, sender))
        {
            _dragDrawer = null;
            return;
        }
        _drawerDragStart = e.GetPosition(this);
        _dragDrawer = (sender as FrameworkElement)?.DataContext as Drawer;
    }

    /// <summary>True when the hit element sits inside a button nested within the row button.</summary>
    private static bool PressedOnNestedButton(DependencyObject? origin, object rowButton)
    {
        var node = origin;
        while (node != null && node != rowButton)
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase) return true;
            // Text hits (Run/InlineText) aren't visuals — VisualTreeHelper.GetParent would throw.
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private void DrawerRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragDrawer == null || e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _drawerDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _drawerDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var drawer = _dragDrawer;
        _dragDrawer = null;
        var data = new DataObject();
        data.SetData(DrawerDragFormat, drawer.Id);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
    }

    private void DrawerRow_DragOver(object sender, DragEventArgs e)
    {
        DragDropEffects effects;
        if (e.Data.GetData(DrawerDragFormat) is string draggedId)
        {
            // Reorder is valid only when hovering a *different* drawer than the one being dragged.
            var over = (sender as FrameworkElement)?.DataContext as Drawer;
            effects = over != null && over.Id != draggedId ? DragDropEffects.Move : DragDropEffects.None;
        }
        else
        {
            effects = AcceptsDrop(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        e.Effects = effects;
        if (sender is DependencyObject row)
            Behaviors.DragDropCue.SetIsDropTarget(row, effects != DragDropEffects.None);
        e.Handled = true;
    }

    private void DrawerRow_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is DependencyObject row) Behaviors.DragDropCue.SetIsDropTarget(row, false);
    }

    private void DrawerRow_Drop(object sender, DragEventArgs e)
    {
        if (sender is DependencyObject row) Behaviors.DragDropCue.SetIsDropTarget(row, false);
        if (ViewModel is not { } vm) return;
        if ((sender as FrameworkElement)?.DataContext is not Drawer drawer) return;
        if (e.Data.GetData(DrawerDragFormat) is string draggedId)
        {
            vm.ReorderDrawer(draggedId, drawer);
            e.Handled = true;
            return;
        }
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
        if (e.Data.GetDataPresent(ClipDragFormat)) return; // internal clip drag that missed a drawer
        if (e.Data.GetDataPresent(DrawerDragFormat)) return; // drawer reorder that missed a row
        HandleExternalDrop(e, null);
    }

    /// <summary>Routes a drop to the right capture path. Order matters: files, then media URL, then text.</summary>
    private void HandleExternalDrop(DragEventArgs e, Drawer? target)
    {
        if (ViewModel is not { } vm) return;

        if (e.Data.GetData(ClipDragFormat) is string idBlob)
        {
            vm.MoveClipsToDrawer(idBlob.Split('\n', StringSplitOptions.RemoveEmptyEntries), target ?? vm.SessionDrawer);
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
        if (e.Data.GetDataPresent(DrawerDragFormat)) return; // internal drawer reorder
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
