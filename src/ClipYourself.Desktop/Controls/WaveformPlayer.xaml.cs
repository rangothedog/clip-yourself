using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;

namespace ClipYourself.Desktop.Controls;

/// <summary>
/// Compact audio player: a decoded-peaks waveform when idle, and a live LED
/// frequency analyzer (blue on black) while playing. Click-to-seek, play/pause.
/// </summary>
public partial class WaveformPlayer : UserControl
{
    private const int Bars = 96;

    // Analyzer geometry
    private const int Columns = 20;
    private const int Leds = 10;

    private static readonly Brush LedOff = Frozen("#FF1B2129");
    private static readonly Brush LedOn = Frozen("#FF4F8CFF");
    private static readonly Brush LedPeak = Frozen("#FFBFD8FF");

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(WaveformPlayer),
        new PropertyMetadata(null, OnSourceChanged));

    private readonly Border[][] _leds = new Border[Columns][];
    private readonly float[] _levels = new float[Columns];
    private int[] _bandStart = Array.Empty<int>();
    private int[] _bandEnd = Array.Empty<int>();
    private float _runningMax = 1e-4f;

    private AudioFileReader? _reader;
    private WaveOutEvent? _waveOut;
    private DispatcherTimer? _timer;
    private TimeSpan _duration = TimeSpan.Zero;
    private bool _isPlaying;
    private volatile bool _framePending;

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public WaveformPlayer()
    {
        InitializeComponent();
        BuildAnalyzer();
        Unloaded += (_, _) => StopAndRelease();
    }

    private static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private void BuildAnalyzer()
    {
        for (var col = 0; col < Columns; col++)
        {
            var stack = new UniformGrid { Rows = Leds, Columns = 1, Margin = new Thickness(0.7, 0, 0.7, 0) };
            _leds[col] = new Border[Leds];
            for (var row = 0; row < Leds; row++)
            {
                var led = new Border
                {
                    Background = LedOff,
                    CornerRadius = new CornerRadius(0.5),
                    Margin = new Thickness(0, 0.5, 0, 0.5)
                };
                _leds[col][row] = led;
                stack.Children.Add(led); // row 0 = top, Leds-1 = bottom
            }
            AnalyzerGrid.Children.Add(stack);
        }
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (WaveformPlayer)d;
        control.StopAndRelease();
        control.LoadWaveform(e.NewValue as string);
    }

    // ----- static waveform (idle state) -----

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

    // ----- playback + live analyzer -----

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            _waveOut?.Pause();
            SetPlaying(false);
            return;
        }

        var path = Source;
        if (path == null || !File.Exists(path)) return;

        try
        {
            if (_waveOut == null)
            {
                _reader = new AudioFileReader(path);
                _duration = _reader.TotalTime;
                EnsureBands();

                var spectrum = new SpectrumSampleProvider(_reader.ToSampleProvider());
                spectrum.FftCalculated += OnFftCalculated;

                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Init(spectrum);
            }
            _waveOut.Play();
            SetPlaying(true);

            _timer ??= CreateTimer();
            _timer.Start();
        }
        catch
        {
            StopAndRelease();
            FallbackText.Visibility = Visibility.Visible;
        }
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => { if (_reader != null) UpdateProgress(_reader.CurrentTime); };
        return timer;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // Fires on natural end (we Pause, never Stop, for pause) — reset to the top.
        Dispatcher.BeginInvoke(() =>
        {
            SetPlaying(false);
            try { if (_reader != null) _reader.CurrentTime = TimeSpan.Zero; } catch { }
            UpdateProgress(TimeSpan.Zero);
            ClearAnalyzer();
        });
    }

    private void OnFftCalculated(float[] mags)
    {
        // Coalesce: drop frames if the UI hasn't drawn the last one yet.
        if (_framePending) return;
        _framePending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _framePending = false;
            if (_isPlaying) RenderSpectrum(mags);
        }, DispatcherPriority.Render);
    }

    private void EnsureBands()
    {
        if (_bandStart.Length == Columns) return;
        _bandStart = new int[Columns];
        _bandEnd = new int[Columns];
        var bins = SpectrumSampleProvider.FftLength / 2;
        // Log-spaced bands from bin 1 to the top usable bin.
        double min = Math.Log(1), max = Math.Log(bins - 1);
        for (var i = 0; i < Columns; i++)
        {
            var lo = (int)Math.Exp(min + (max - min) * i / Columns);
            var hi = (int)Math.Exp(min + (max - min) * (i + 1) / Columns);
            _bandStart[i] = Math.Clamp(lo, 1, bins - 1);
            _bandEnd[i] = Math.Clamp(Math.Max(hi, lo + 1), 2, bins);
        }
    }

    private void RenderSpectrum(float[] mags)
    {
        Span<float> raw = stackalloc float[Columns];
        var frameMax = 1e-4f;
        for (var col = 0; col < Columns; col++)
        {
            float peak = 0;
            for (var b = _bandStart[col]; b < _bandEnd[col]; b++)
                if (mags[b] > peak) peak = mags[b];
            raw[col] = peak;
            if (peak > frameMax) frameMax = peak;
        }

        // Auto-gain: normalize to a slowly-decaying running max so the meter
        // stays lively regardless of the source's absolute level.
        _runningMax = Math.Max(frameMax, _runningMax * 0.995f);

        for (var col = 0; col < Columns; col++)
        {
            // sqrt gives a perceptual curve so quiet bands still show some LEDs.
            var norm = (float)Math.Sqrt(Math.Clamp(raw[col] / _runningMax, 0, 1));
            _levels[col] = norm > _levels[col] ? norm : _levels[col] * 0.78f + norm * 0.22f;

            var lit = (int)Math.Round(_levels[col] * Leds);
            var firstLit = Leds - lit;
            for (var row = 0; row < Leds; row++)
            {
                var brush = row < firstLit ? LedOff : (row == firstLit ? LedPeak : LedOn);
                if (!ReferenceEquals(_leds[col][row].Background, brush))
                    _leds[col][row].Background = brush;
            }
        }
    }

    private void ClearAnalyzer()
    {
        _runningMax = 1e-4f;
        for (var col = 0; col < Columns; col++)
        {
            _levels[col] = 0;
            for (var row = 0; row < Leds; row++)
                if (!ReferenceEquals(_leds[col][row].Background, LedOff))
                    _leds[col][row].Background = LedOff;
        }
    }

    private void WaveGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_reader == null || _duration == TimeSpan.Zero) return;
        var fraction = Math.Clamp(e.GetPosition(WaveGrid).X / Math.Max(1, WaveGrid.ActualWidth), 0, 1);
        try
        {
            // Pause the audio thread's reads before repositioning the shared reader.
            var wasPlaying = _isPlaying;
            if (wasPlaying) _waveOut?.Pause();
            _reader.CurrentTime = TimeSpan.FromMilliseconds(_duration.TotalMilliseconds * fraction);
            if (wasPlaying) _waveOut?.Play();
            UpdateProgress(_reader.CurrentTime);
        }
        catch { }
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

        // Swap the waveform for the live analyzer while playing.
        AnalyzerHost.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
        WavePolygon.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        ProgressRect.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;

        if (PlayButton.Template?.FindName("Glyph", PlayButton) is TextBlock glyph)
            glyph.Text = playing ? "⏸" : "▶";
    }

    private void StopAndRelease()
    {
        _timer?.Stop();
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            try { _waveOut.Stop(); } catch { }
            _waveOut.Dispose();
            _waveOut = null;
        }
        _reader?.Dispose();
        _reader = null;
        SetPlaying(false);
        ClearAnalyzer();
    }

    private static string FormatTime(TimeSpan time)
        => time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
}
