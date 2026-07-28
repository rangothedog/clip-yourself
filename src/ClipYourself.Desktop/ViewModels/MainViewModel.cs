using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using ClipYourself.Core.Models;
using ClipYourself.Core.Services;
using ClipYourself.Desktop.Services;

namespace ClipYourself.Desktop.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly StorageService _storage;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _statusTimer;
    private DateTime _suppressCaptureUntil = DateTime.MinValue;

    private Drawer? _openDrawer;
    private bool _isSettingsOpen;
    private string _statusText = string.Empty;
    private bool _hotkeyRegistered = true;

    public AppSettings Settings { get; }

    /// <summary>The drawer automatically created for this session; new clips land here by default.</summary>
    public Drawer SessionDrawer { get; }

    /// <summary>All other drawers (previous sessions, projects, categories…).</summary>
    public ObservableCollection<Drawer> Drawers { get; } = new();

    public MainViewModel(StorageService storage)
    {
        _storage = storage;
        Settings = storage.LoadSettings();

        var loaded = storage.LoadDrawers();
        storage.SweepOrphanBlobs(loaded);
        foreach (var drawer in loaded)
        {
            Drawers.Add(drawer);
            HookDrawer(drawer);
        }

        SessionDrawer = new Drawer
        {
            Name = $"Session — {DateTime.Now:MMM d, h:mm tt}",
            IsSession = true,
            MaxClips = Settings.DefaultMaxClips,
            MaxSizeMB = Settings.DefaultMaxSizeMB
        };
        HookDrawer(SessionDrawer);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveAll(); };

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); StatusText = string.Empty; };

        NewDrawerCommand = new RelayCommand(_ => NewDrawer());
        OpenDrawerCommand = new RelayCommand(p => { if (p is Drawer d) OpenDrawer = d; });
        CloseReelCommand = new RelayCommand(_ => OpenDrawer = null);
        DeleteDrawerCommand = new RelayCommand(p => { if (p is Drawer d) DeleteDrawer(d); });
        ClearDrawerCommand = new RelayCommand(p => { if (p is Drawer d) ClearDrawer(d); });
        DeleteClipCommand = new RelayCommand(p => { if (p is ClipItem c) DeleteClip(c); });
        CopyClipCommand = new RelayCommand(p => { if (p is ClipItem c) CopyClip(c); });
        ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
        ExitCommand = new RelayCommand(_ => App.ExitApp());
    }

    public RelayCommand NewDrawerCommand { get; }
    public RelayCommand OpenDrawerCommand { get; }
    public RelayCommand CloseReelCommand { get; }
    public RelayCommand DeleteDrawerCommand { get; }
    public RelayCommand ClearDrawerCommand { get; }
    public RelayCommand DeleteClipCommand { get; }
    public RelayCommand CopyClipCommand { get; }
    public RelayCommand ToggleSettingsCommand { get; }
    public RelayCommand ExitCommand { get; }

    /// <summary>Drawer shown in the reel view; while open it is also the capture target.</summary>
    public Drawer? OpenDrawer
    {
        get => _openDrawer;
        set
        {
            if (_openDrawer == value) return;
            _openDrawer = value;
            Raise(nameof(OpenDrawer));
        }
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set { if (_isSettingsOpen == value) return; _isSettingsOpen = value; Raise(nameof(IsSettingsOpen)); }
    }

    public string StatusText
    {
        get => _statusText;
        set { if (_statusText == value) return; _statusText = value; Raise(nameof(StatusText)); }
    }

    public bool HotkeyRegistered
    {
        get => _hotkeyRegistered;
        set { _hotkeyRegistered = value; Raise(nameof(HotkeyRegistered)); }
    }

    public bool PersistClips
    {
        get => Settings.PersistClips;
        set
        {
            if (Settings.PersistClips == value) return;
            Settings.PersistClips = value;
            Raise(nameof(PersistClips));
            if (value)
            {
                SaveAll();
                ShowStatus("Clips will be saved between sessions");
            }
            else
            {
                // Opting out removes what was previously written to disk.
                _storage.DeleteAllDrawerFiles();
                _storage.SaveSettings(Settings);
                ShowStatus("Saved clips removed from disk");
            }
        }
    }

    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set
        {
            if (Settings.AlwaysOnTop == value) return;
            Settings.AlwaysOnTop = value;
            Raise(nameof(AlwaysOnTop));
            ScheduleSave();
        }
    }

    public int DefaultMaxClips
    {
        get => Settings.DefaultMaxClips;
        set { Settings.DefaultMaxClips = Math.Max(1, value); Raise(nameof(DefaultMaxClips)); ScheduleSave(); }
    }

    public int DefaultMaxSizeMB
    {
        get => Settings.DefaultMaxSizeMB;
        set { Settings.DefaultMaxSizeMB = Math.Max(1, value); Raise(nameof(DefaultMaxSizeMB)); ScheduleSave(); }
    }

    private IEnumerable<Drawer> AllDrawers()
    {
        yield return SessionDrawer;
        foreach (var drawer in Drawers) yield return drawer;
    }

    // ----- clipboard capture -----

    public void OnClipboardChanged()
    {
        if (DateTime.Now < _suppressCaptureUntil) return;
        Capture();
    }

    public void CaptureInitial() => Capture();

    private void Capture()
    {
        var item = ClipCapture.TryCapture(_storage);
        if (item == null) return;

        var target = OpenDrawer ?? SessionDrawer;
        var evicted = DrawerOps.AddClip(target, item);
        if (evicted.Count > 0) _storage.SweepOrphanBlobs(AllDrawers());
        ScheduleSave();
    }

    // ----- clip actions -----

    private void CopyClip(ClipItem clip)
    {
        _suppressCaptureUntil = DateTime.Now.AddMilliseconds(800);
        if (ClipboardWriter.TryWrite(clip))
        {
            var owner = FindOwner(clip);
            if (owner != null) DrawerOps.Touch(owner, clip);
            ShowStatus("Copied ✓");
            ScheduleSave();
        }
        else
        {
            ShowStatus("Copy failed — content unavailable");
        }
    }

    private void DeleteClip(ClipItem clip)
    {
        var owner = FindOwner(clip);
        if (owner == null) return;
        owner.Clips.Remove(clip);
        _storage.SweepOrphanBlobs(AllDrawers());
        ScheduleSave();
    }

    private Drawer? FindOwner(ClipItem clip)
        => AllDrawers().FirstOrDefault(d => d.Clips.Contains(clip));

    // ----- drawer actions -----

    private void NewDrawer()
    {
        var drawer = new Drawer
        {
            Name = "New Drawer",
            MaxClips = Settings.DefaultMaxClips,
            MaxSizeMB = Settings.DefaultMaxSizeMB
        };
        Drawers.Insert(0, drawer);
        HookDrawer(drawer);
        OpenDrawer = drawer;
        IsSettingsOpen = false;
    }

    private void DeleteDrawer(Drawer drawer)
    {
        if (drawer == SessionDrawer) return;
        Drawers.Remove(drawer);
        _storage.DeleteDrawerFile(drawer.Id);
        if (OpenDrawer == drawer) OpenDrawer = null;
        _storage.SweepOrphanBlobs(AllDrawers());
    }

    private void ClearDrawer(Drawer drawer)
    {
        drawer.Clips.Clear();
        _storage.DeleteDrawerFile(drawer.Id);
        _storage.SweepOrphanBlobs(AllDrawers());
        ScheduleSave();
    }

    private void HookDrawer(Drawer drawer)
    {
        drawer.PropertyChanged += (_, _) => ScheduleSave();
        drawer.Clips.CollectionChanged += OnAnyClipsChanged;
    }

    private void OnAnyClipsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleSave();

    // ----- persistence -----

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveAll()
    {
        try
        {
            _storage.SaveSettings(Settings);
            if (!Settings.PersistClips) return;
            foreach (var drawer in AllDrawers()) _storage.SaveDrawer(drawer);
        }
        catch
        {
            // Persistence is best-effort; never take the app down over a save.
        }
    }

    private void ShowStatus(string message)
    {
        StatusText = message;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
