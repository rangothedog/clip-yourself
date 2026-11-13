using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClipYourself.Desktop.Controls;

/// <summary>
/// A click-through, never-activating overlay that draws a focus reticle around a
/// rectangle on screen (another app's text field). Positioned in raw screen pixels so it
/// lines up with UI Automation bounds regardless of DPI.
/// </summary>
public sealed class ReticleWindow : Window
{
    private const double PadDip = 6;     // reticle sits this far outside the field
    private const double LabelDip = 20;  // "Type here" / "Paste here" tag above the frame

    public const string TypeLabel = "⌨ Type here";
    public const string PasteLabel = "📋 Paste here";

    private readonly TextBlock _label = new()
    {
        Foreground = Brushes.White,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };

    public ReticleWindow(string label = TypeLabel)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        Title = "Clip Yourself reticle";
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
        Width = 1;
        Height = 1;
        _label.Text = label;
        Content = BuildVisual(_label);

        // Styles must be in place before the first Show, or it would steal activation once.
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>The tag text above the frame (switchable mid-drag).</summary>
    public string Label
    {
        get => _label.Text;
        set { if (_label.Text != value) _label.Text = value; }
    }

    /// <summary>Wraps the reticle around <paramref name="bounds"/> (screen pixels) and shows it.</summary>
    public void ShowAround(Rect bounds)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var pad = PadDip * scale;
        var label = LabelDip * scale;

        var left = (int)Math.Floor(bounds.Left - pad);
        var top = (int)Math.Floor(bounds.Top - pad - label);
        var width = (int)Math.Ceiling(bounds.Width + pad * 2);
        var height = (int)Math.Ceiling(bounds.Height + pad * 2 + label);

        if (!IsVisible) Show(); // parked off-screen until the move below lands it
        SetWindowPos(hwnd, HWND_TOPMOST, left, top, width, height, SWP_NOACTIVATE);
    }

    private static UIElement BuildVisual(TextBlock label)
    {
        var accent = (Brush)Application.Current.FindResource("AccentBrush");
        var root = new Grid();

        // Field outline with a faint wash so the target reads clearly on any background.
        root.Children.Add(new Border
        {
            Margin = new Thickness(PadDip / 2, LabelDip + PadDip / 2, PadDip / 2, PadDip / 2),
            BorderBrush = accent,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromArgb(0x1E, 0x4F, 0x8C, 0xFF))
        });

        // Pulsing corner brackets.
        var brackets = new Grid { Margin = new Thickness(0, LabelDip, 0, 0) };
        brackets.Children.Add(Bracket(accent, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(3, 3, 0, 0)));
        brackets.Children.Add(Bracket(accent, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 3, 3, 0)));
        brackets.Children.Add(Bracket(accent, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(3, 0, 0, 3)));
        brackets.Children.Add(Bracket(accent, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 3, 3)));
        brackets.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(550))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        });
        root.Children.Add(brackets);

        // Action tag riding the top-left corner.
        root.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Height = LabelDip - 2,
            Background = accent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 0, 7, 0),
            Child = label
        });
        return root;
    }

    private static Border Bracket(Brush brush, HorizontalAlignment h, VerticalAlignment v, Thickness edges) => new()
    {
        Width = 14,
        Height = 14,
        HorizontalAlignment = h,
        VerticalAlignment = v,
        BorderBrush = brush,
        BorderThickness = edges,
        CornerRadius = new CornerRadius(
            edges.Left > 0 && edges.Top > 0 ? 4 : 0,
            edges.Right > 0 && edges.Top > 0 ? 4 : 0,
            edges.Right > 0 && edges.Bottom > 0 ? 4 : 0,
            edges.Left > 0 && edges.Bottom > 0 ? 4 : 0)
    };

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
