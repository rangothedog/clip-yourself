using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ClipYourself.Desktop.Services;

/// <summary>
/// DAW Bridge (route "watched folders"): watches export directories for new
/// audio files and hands them off once they stop growing and can be opened
/// exclusively — DAWs write exports incrementally, so a file is only "ready"
/// when the writer lets go of it.
/// </summary>
public sealed class DawBridgeService : IDisposable
{
    private const int MaxStabilityChecks = 40; // ~50s of retries per file

    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingFile> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _poll;
    private readonly Action<string> _onAudioFileReady;
    private bool _disposed;

    private sealed class PendingFile
    {
        public long LastSize = -1;
        public int Checks;
    }

    public DawBridgeService(Action<string> onAudioFileReady)
    {
        _onAudioFileReady = onAudioFileReady;
        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _poll.Tick += (_, _) => PollPending();
    }

    public void UpdateFolders(IEnumerable<string> folders)
    {
        var wanted = folders
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var stale in _watchers.Keys.Except(wanted, StringComparer.OrdinalIgnoreCase).ToList())
        {
            _watchers[stale].Dispose();
            _watchers.Remove(stale);
        }

        foreach (var folder in wanted)
        {
            if (_watchers.ContainsKey(folder)) continue;
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Created += (_, e) => Enqueue(e.FullPath);
                watcher.Changed += (_, e) => Enqueue(e.FullPath);
                watcher.Renamed += (_, e) => Enqueue(e.FullPath);
                _watchers[folder] = watcher;
            }
            catch
            {
                // Folder vanished or is inaccessible; it just isn't watched.
            }
        }
    }

    private void Enqueue(string path)
    {
        if (_disposed || !ClipCapture.IsAudioFile(path)) return;

        // FileSystemWatcher raises on pool threads; all state lives on the UI thread.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            if (!_pending.TryGetValue(path, out var state))
            {
                state = new PendingFile();
                _pending[path] = state;
            }
            state.Checks = 0; // fresh activity restarts the stability clock
            _poll.Start();
        });
    }

    private void PollPending()
    {
        foreach (var (path, state) in _pending.ToList())
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    _pending.Remove(path);
                    continue;
                }

                if (info.Length > 0 && info.Length == state.LastSize && CanOpenExclusively(path))
                {
                    _pending.Remove(path);
                    _onAudioFileReady(path);
                }
                else
                {
                    state.LastSize = info.Length;
                    if (++state.Checks > MaxStabilityChecks) _pending.Remove(path);
                }
            }
            catch
            {
                _pending.Remove(path);
            }
        }
        if (_pending.Count == 0) _poll.Stop();
    }

    private static bool CanOpenExclusively(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _poll.Stop();
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _watchers.Clear();
        _pending.Clear();
    }
}
