using System.IO;
using System.Windows;
using System.Windows.Threading;
using ClipYourself.Core.Services;
using ClipYourself.Desktop.Interop;
using ClipYourself.Desktop.ViewModels;

namespace ClipYourself.Desktop;

public partial class App : Application
{
    private const string MutexName = @"Local\ClipYourself.Singleton";
    private const string ShowEventName = @"Local\ClipYourself.Show";

    public static bool IsExiting { get; private set; }

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private ClipboardMonitor? _monitor;
    private HotkeyService? _hotkey;
    private MainViewModel? _viewModel;
    private bool _cleanedUp;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Safety net: a resident clipboard manager must not lose its in-memory
        // clips to a stray UI-thread exception. Log it, save, and stay alive.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _mutex = new Mutex(true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            // Another instance is already running: ask it to show itself and bow out.
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ShowEventName);
                existing.Set();
            }
            catch { }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                if (IsExiting) break;
                Dispatcher.BeginInvoke(ShowSidebar);
            }
        })
        { IsBackground = true, Name = "ClipYourself.ShowListener" };
        listener.Start();

        var storage = new StorageService();
        _viewModel = new MainViewModel(storage);

        var window = new MainWindow { DataContext = _viewModel };
        MainWindow = window;
        window.Show();

        _monitor = new ClipboardMonitor(window);
        _monitor.ClipboardChanged += _viewModel.OnClipboardChanged;

        _hotkey = new HotkeyService(window, _viewModel.Settings.Hotkey, ToggleSidebar);
        _viewModel.HotkeyRegistered = _hotkey.Registered;
        _viewModel.RebindHotkey = gesture => _hotkey.TryRebind(gesture);

        _viewModel.CaptureInitial();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClipYourself", "crash.log");
            File.AppendAllText(log, $"[{DateTime.Now:O}] {e.Exception}\n\n");
            _viewModel?.SaveAll();
        }
        catch { }
        // Keep the sidebar (and the user's clips) alive rather than crashing.
        e.Handled = true;
    }

    private void ShowSidebar()
    {
        var window = MainWindow;
        if (window == null) return;
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void ToggleSidebar()
    {
        var window = MainWindow;
        if (window == null) return;
        if (window.IsVisible && window.IsActive) window.Hide();
        else ShowSidebar();
    }

    public static void ExitApp()
    {
        IsExiting = true;
        ((App)Current).Cleanup();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    private void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        IsExiting = true;

        (MainWindow as MainWindow)?.ReleaseAppBar();
        _viewModel?.SaveAll();
        _viewModel?.Shutdown();
        _monitor?.Dispose();
        _hotkey?.Dispose();
        _showEvent?.Set();

        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
    }
}
