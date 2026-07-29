using NAudio.Dsp;
using NAudio.Wave;

namespace ClipYourself.Desktop.Controls;

/// <summary>
/// Passes audio through unchanged while tapping it for a live FFT — feeds the
/// real-time frequency analyzer. Raises FftCalculated (on the audio thread)
/// with magnitude bins each time an FFT block fills.
/// </summary>
public sealed class SpectrumSampleProvider : ISampleProvider
{
    public const int FftLength = 1024;
    private const int FftLog2 = 10; // log2(1024)

    private readonly ISampleProvider _source;
    private readonly Complex[] _fft = new Complex[FftLength];
    private readonly int _channels;
    private int _pos;

    /// <summary>Fires with FftLength/2 magnitude bins. Raised on the audio thread.</summary>
    public event Action<float[]>? FftCalculated;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public SpectrumSampleProvider(ISampleProvider source)
    {
        _source = source;
        _channels = Math.Max(1, source.WaveFormat.Channels);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (var n = 0; n + _channels <= read; n += _channels)
        {
            float mono = 0;
            for (var c = 0; c < _channels; c++) mono += buffer[offset + n + c];
            mono /= _channels;

            _fft[_pos].X = (float)(mono * FastFourierTransform.HammingWindow(_pos, FftLength));
            _fft[_pos].Y = 0;
            _pos++;

            if (_pos >= FftLength)
            {
                _pos = 0;
                FastFourierTransform.FFT(true, FftLog2, _fft);
                var mags = new float[FftLength / 2];
                for (var i = 0; i < mags.Length; i++)
                {
                    mags[i] = (float)Math.Sqrt(_fft[i].X * _fft[i].X + _fft[i].Y * _fft[i].Y);
                }
                FftCalculated?.Invoke(mags);
            }
        }
        return read;
    }
}
