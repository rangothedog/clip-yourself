using System.Runtime.InteropServices;
using ClipYourself.Desktop.Controls;

namespace ClipYourself.Desktop.Interop;

/// <summary>
/// One drag of the "type into a field" picker: hit-tests the screen under the cursor as
/// it moves (off the UI thread, at most one query in flight) and keeps a reticle wrapped
/// around whatever editable text control is underneath.
/// </summary>
public sealed class TextFieldPicker : IDisposable
{
    private readonly ReticleWindow _reticle = new();
    private (int X, int Y) _pending;
    private bool _hasPending;
    private bool _busy;
    private bool _disposed;

    /// <summary>Current cursor position in screen pixels (same space as UI Automation bounds).</summary>
    public static (int X, int Y) CursorPosition()
    {
        GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    /// <summary>Call on every mouse move during the drag.</summary>
    public void Track()
    {
        if (_disposed) return;
        _pending = CursorPosition();
        _hasPending = true;
        if (!_busy) Pump();
    }

    /// <summary>The drag ended: hit-test the release point itself (the last hover may be stale).</summary>
    public async Task<TextFieldTarget?> CompleteAsync()
    {
        var (x, y) = CursorPosition();
        _disposed = true;
        _reticle.Hide();
        return await Task.Run(() => TextFieldTarget.FromPoint(x, y));
    }

    private async void Pump()
    {
        _busy = true;
        try
        {
            while (_hasPending && !_disposed)
            {
                _hasPending = false;
                var (x, y) = _pending;
                var target = await Task.Run(() => TextFieldTarget.FromPoint(x, y));
                if (_disposed) break;
                if (target != null) _reticle.ShowAround(target.Bounds);
                else _reticle.Hide();
            }
        }
        finally { _busy = false; }
    }

    public void Dispose()
    {
        _disposed = true;
        _reticle.Close();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
}
