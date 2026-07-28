using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ClipYourself.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => DockRight();
        Closing += OnClosing;
    }

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
}
