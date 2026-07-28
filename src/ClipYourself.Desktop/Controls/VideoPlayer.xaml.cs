using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ClipYourself.Desktop.Controls;

/// <summary>
/// Small inline video preview: renders the first frame with a play badge,
/// click to play/pause. Falls back to an icon if the codec can't decode.
/// </summary>
public partial class VideoPlayer : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(VideoPlayer),
        new PropertyMetadata(null, OnSourceChanged));

    private readonly DispatcherTimer _timer;
    private bool _playing;
    private bool _opened;
    private bool _primingFrame;

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public VideoPlayer()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateTime();
        Unloaded += (_, _) => Release();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VideoPlayer)d;
        control.Release();
        control.Load(e.NewValue as string);
    }

    private void Load(string? path)
    {
        Fallback.Visibility = Visibility.Collapsed;
        PlayBadge.Visibility = Visibility.Visible;
        TimeText.Text = "";
        _opened = false;
        _playing = false;

        if (path == null || !File.Exists(path))
        {
            ShowFallback();
            return;
        }

        Media.Source = new Uri(path);
        // Play briefly so the first frame renders, then pause on MediaOpened.
        _primingFrame = true;
        Media.Play();
    }

    private void Media_MediaOpened(object sender, RoutedEventArgs e)
    {
        _opened = true;
        UpdateTime();
        if (_primingFrame)
        {
            _primingFrame = false;
            Media.Pause();
            Media.Position = TimeSpan.Zero;
        }
    }

    private void Media_MediaEnded(object sender, RoutedEventArgs e)
    {
        Media.Pause();
        Media.Position = TimeSpan.Zero;
        SetPlaying(false);
    }

    private void Media_MediaFailed(object sender, ExceptionRoutedEventArgs e) => ShowFallback();

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_opened) return;
        if (_playing) { Media.Pause(); SetPlaying(false); }
        else { Media.Play(); SetPlaying(true); }
        // Don't let the click bubble up to the card's copy-to-clipboard button.
        e.Handled = true;
    }

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        PlayGlyph.Text = playing ? "❚❚" : "▶";
        PlayGlyph.Margin = playing ? new Thickness(0) : new Thickness(3, 0, 0, 0);
        PlayBadge.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        if (playing) _timer.Start(); else _timer.Stop();
    }

    private void UpdateTime()
    {
        if (!_opened) return;
        var duration = Media.NaturalDuration.HasTimeSpan ? Media.NaturalDuration.TimeSpan : TimeSpan.Zero;
        TimeText.Text = duration > TimeSpan.Zero
            ? $"{Format(Media.Position)} / {Format(duration)}"
            : Format(Media.Position);
    }

    private void ShowFallback()
    {
        Media.Visibility = Visibility.Collapsed;
        PlayBadge.Visibility = Visibility.Collapsed;
        TimeText.Text = "";
        Fallback.Visibility = Visibility.Visible;
    }

    private void Release()
    {
        _timer.Stop();
        try
        {
            Media.Stop();
            Media.Close();
            Media.Source = null;
        }
        catch { }
        _opened = false;
        _playing = false;
    }

    private static string Format(TimeSpan t)
        => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
}
