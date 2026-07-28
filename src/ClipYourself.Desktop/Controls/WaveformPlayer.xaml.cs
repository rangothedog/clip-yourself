using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;

namespace ClipYourself.Desktop.Controls;

/// <summary>Compact audio player: waveform rendered from decoded peaks, click-to-seek, play/pause.</summary>
public partial class WaveformPlayer : UserControl
{
    private const int Bars = 96;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(WaveformPlayer),
        new PropertyMetadata(null, OnSourceChanged));

    private MediaPlayer? _player;
    private DispatcherTimer? _timer;
    private TimeSpan _duration = TimeSpan.Zero;
    private bool _isPlaying;
    private bool _mediaOpened;

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public WaveformPlayer()
    {
        InitializeComponent();
        Unloaded += (_, _) => StopAndRelease();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (WaveformPlayer)d;
        control.StopAndRelease();
        control.LoadWaveform(e.NewValue as string);
    }

    private void LoadWaveform(string? path)
    {
        WavePolygon.Points = new PointCollection();
        FallbackText.Visibility = Visibility.Collapsed;
        TimeText.Text = "";
        if (path == null || !File.Exists(path))
        {
            FallbackText.Visibility = Visibility.Visible;
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var (peaks, duration) = ReadPeaks(path);
                Dispatcher.BeginInvoke(() =>
                {
                    if (Source != path) return;
                    _duration = duration;
                    WavePolygon.Points = BuildPolygon(peaks);
                    TimeText.Text = FormatTime(TimeSpan.Zero) + " / " + FormatTime(duration);
                });
            }
            catch
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (Source == path) FallbackText.Visibility = Visibility.Visible;
                });
            }
        });
    }

    private static (float[] peaks, TimeSpan duration) ReadPeaks(string path)
    {
        using var reader = new AudioFileReader(path);
        var peaks = new float[Bars];
        var totalFloats = Math.Max(1, reader.Length / 4);
        var perBar = Math.Max(1, totalFloats / Bars);
        var buffer = new float[reader.WaveFormat.SampleRate * Math.Max(1, reader.WaveFormat.Channels)];

        long index = 0;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var bar = (int)Math.Min(Bars - 1, index++ / perBar);
                var value = Math.Abs(buffer[i]);
                if (value > peaks[bar]) peaks[bar] = value;
            }
        }
        return (peaks, reader.TotalTime);
    }

    private static PointCollection BuildPolygon(float[] peaks)
    {
        // Mirrored bar silhouette in a normalized 0..Bars × 0..30 space; Stretch=Fill scales it.
        var points = new PointCollection();
        for (var i = 0; i < peaks.Length; i++)
        {
            var amplitude = Math.Max(peaks[i], 0.04f) * 14;
            points.Add(new Point(i, 15 - amplitude));
        }
        for (var i = peaks.Length - 1; i >= 0; i--)
        {
            var amplitude = Math.Max(peaks[i], 0.04f) * 14;
            points.Add(new Point(i, 15 + amplitude));
        }
        points.Freeze();
        return points;
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            _player?.Pause();
            SetPlaying(false);
            return;
        }

        var path = Source;
        if (path == null || !File.Exists(path)) return;

        if (_player == null)
        {
            _player = new MediaPlayer();
            _player.MediaOpened += (_, _) =>
            {
                _mediaOpened = true;
                if (_player.NaturalDuration.HasTimeSpan) _duration = _player.NaturalDuration.TimeSpan;
            };
            _player.MediaEnded += (_, _) =>
            {
                _player.Stop();
                SetPlaying(false);
                UpdateProgress(TimeSpan.Zero);
            };
            _player.MediaFailed += (_, _) => SetPlaying(false);
            _player.Open(new Uri(path));
        }

        _player.Play();
        SetPlaying(true);

        if (_timer == null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += (_, _) => { if (_player != null) UpdateProgress(_player.Position); };
        }
        _timer.Start();
    }

    private void WaveGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_player == null || !_mediaOpened || _duration == TimeSpan.Zero) return;
        var fraction = Math.Clamp(e.GetPosition(WaveGrid).X / Math.Max(1, WaveGrid.ActualWidth), 0, 1);
        _player.Position = TimeSpan.FromMilliseconds(_duration.TotalMilliseconds * fraction);
        UpdateProgress(_player.Position);
        e.Handled = true;
    }

    private void UpdateProgress(TimeSpan position)
    {
        var fraction = _duration.TotalMilliseconds > 0
            ? Math.Clamp(position.TotalMilliseconds / _duration.TotalMilliseconds, 0, 1)
            : 0;
        ProgressRect.Width = fraction * WaveGrid.ActualWidth;
        TimeText.Text = FormatTime(position) + " / " + FormatTime(_duration);
    }

    private void SetPlaying(bool playing)
    {
        _isPlaying = playing;
        if (playing) _timer?.Start(); else _timer?.Stop();
        if (PlayButton.Template?.FindName("Glyph", PlayButton) is System.Windows.Controls.TextBlock glyph)
            glyph.Text = playing ? "⏸" : "▶";
    }

    private void StopAndRelease()
    {
        _timer?.Stop();
        if (_player != null)
        {
            _player.Stop();
            _player.Close();
            _player = null;
        }
        _mediaOpened = false;
        SetPlaying(false);
    }

    private static string FormatTime(TimeSpan time)
        => time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
}
