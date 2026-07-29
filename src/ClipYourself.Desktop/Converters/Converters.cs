using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipYourself.Core.Models;

namespace ClipYourself.Desktop.Converters;

/// <summary>Shows the element only when the clip kind is in the comma-separated parameter list.</summary>
public class KindToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ClipKind kind && parameter is string list)
        {
            foreach (var name in list.Split(','))
            {
                if (string.Equals(name.Trim(), kind.ToString(), StringComparison.OrdinalIgnoreCase))
                    return Visibility.Visible;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Colored type badges (TXT/IMG/AUD/FILE) — WPF renders emoji monochrome,
/// so tinted badges are how clip kinds get color on the dark theme.
/// </summary>
public static class KindBadge
{
    public static readonly Brush TextBg = Freeze("#FF23364F");
    public static readonly Brush TextFg = Freeze("#FF7CB1FF");
    public static readonly Brush ImageBg = Freeze("#FF1F3B2C");
    public static readonly Brush ImageFg = Freeze("#FF7CE38B");
    public static readonly Brush AudioBg = Freeze("#FF322A4A");
    public static readonly Brush AudioFg = Freeze("#FFC4B5FD");
    public static readonly Brush VideoBg = Freeze("#FF1F393D");
    public static readonly Brush VideoFg = Freeze("#FF6FD8DE");
    public static readonly Brush FilesBg = Freeze("#FF3D3323");
    public static readonly Brush FilesFg = Freeze("#FFFFD97A");

    private static Brush Freeze(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public class KindToBadgeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ClipKind kind
            ? kind switch
            {
                ClipKind.Text => "TXT",
                ClipKind.Image => "IMG",
                ClipKind.Audio => "AUD",
                ClipKind.Video => "VID",
                ClipKind.Files => "FILE",
                _ => "CLIP"
            }
            : "CLIP";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class KindToBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ClipKind kind
            ? kind switch
            {
                ClipKind.Text => KindBadge.TextBg,
                ClipKind.Image => KindBadge.ImageBg,
                ClipKind.Audio => KindBadge.AudioBg,
                ClipKind.Video => KindBadge.VideoBg,
                ClipKind.Files => KindBadge.FilesBg,
                _ => KindBadge.TextBg
            }
            : KindBadge.TextBg;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class KindToBadgeForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ClipKind kind
            ? kind switch
            {
                ClipKind.Text => KindBadge.TextFg,
                ClipKind.Image => KindBadge.ImageFg,
                ClipKind.Audio => KindBadge.AudioFg,
                ClipKind.Video => KindBadge.VideoFg,
                ClipKind.Files => KindBadge.FilesFg,
                _ => KindBadge.TextFg
            }
            : KindBadge.TextFg;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class KindToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ClipKind kind
            ? kind switch
            {
                ClipKind.Text => "📝",
                ClipKind.Image => "🖼",
                ClipKind.Audio => "🎵",
                ClipKind.Video => "🎬",
                ClipKind.Files => "📁",
                _ => "📋"
            }
            : "📋";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PathToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 480;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime time) return string.Empty;
        var age = DateTime.Now - time;
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m ago";
        if (age < TimeSpan.FromHours(24)) return $"{(int)age.TotalHours}h ago";
        if (age < TimeSpan.FromHours(48)) return "yesterday";
        return time.ToString("MMM d, h:mm tt", culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BytesToSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var bytes = value switch
        {
            long l => l,
            int i => i,
            _ => 0L
        };
        if (bytes <= 0) return "";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Null or empty string → Collapsed; anything else → Visible.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value == null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>3 → "3 clips", 1 → "1 clip".</summary>
public class ClipCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count ? (count == 1 ? "1 clip" : $"{count} clips") : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True → Collapsed, False → Visible.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Zero (or non-numeric) → Visible; used for empty-state hints.</summary>
public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Usage fraction (0–1) → a thermometer color: green when nearly empty, through
/// amber, to red as the drawer fills. Hue swept from 120° (green) to 0° (red).
/// </summary>
public class UsageToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var f = value switch { double d => d, float fl => fl, _ => 0.0 };
        f = Math.Clamp(f, 0, 1);
        var hue = 120.0 * (1.0 - f);          // 120=green → 0=red
        var color = FromHsv(hue, 0.72, 0.90);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        var m = v - c;
        double r, g, b;
        if (h < 60)       { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else              { r = c; g = 0; b = x; }
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
