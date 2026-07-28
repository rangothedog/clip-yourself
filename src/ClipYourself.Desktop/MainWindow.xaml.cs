using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ClipYourself.Core.Models;
using ClipYourself.Desktop.ViewModels;

namespace ClipYourself.Desktop;

public partial class MainWindow : Window
{
    /// <summary>Custom drag format used to file clips into drawers.</summary>
    public const string ClipDragFormat = "ClipYourself.ClipId";

    private Point _dragStart;
    private ClipItem? _dragClip;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => DockRight();
        Closing += OnClosing;

        // A borderless window that gets maximized (Win+Up / snap) covers the whole
        // screen with no way back — undo it immediately and re-dock instead.
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                DockRight();
            }
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

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
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();

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
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(ClipDragFormat, clip.Id), DragDropEffects.Move);
    }

    private void DrawerRow_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ClipDragFormat) ? DragDropEffects.Move
            : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DrawerRow_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if ((sender as FrameworkElement)?.DataContext is not Drawer drawer) return;

        if (e.Data.GetData(ClipDragFormat) is string clipId) vm.MoveClipToDrawer(clipId, drawer);
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files) vm.AddDroppedFiles(files, drawer);
        e.Handled = true;
    }

    private void SessionBar_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ClipDragFormat) ? DragDropEffects.Move
            : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void SessionBar_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Data.GetData(ClipDragFormat) is string clipId) vm.MoveClipToDrawer(clipId, vm.SessionDrawer);
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] files) vm.AddDroppedFiles(files, vm.SessionDrawer);
        e.Handled = true;
    }

    // ----- dropping external content anywhere on the sidebar -----

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Data.GetDataPresent(ClipDragFormat)) return; // internal drag that missed a drawer

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) vm.AddDroppedFiles(files, null);
        else if (e.Data.GetData(DataFormats.UnicodeText) is string text) vm.AddDroppedText(text, null);
    }
}
