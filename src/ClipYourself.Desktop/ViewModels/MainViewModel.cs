using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ClipYourself.Core.Models;
using ClipYourself.Core.Services;
using ClipYourself.Desktop.Services;

namespace ClipYourself.Desktop.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    /// <summary>Custom drag format used to file clips into drawers inside the app.</summary>
    public const string ClipDragFormat = "ClipYourself.ClipId";

    /// <summary>Practical CF_WAVE ceiling — Windows rejects large in-RAM wave payloads.</summary>
    private const long MaxWaveClipboardBytes = 25L * 1024 * 1024;

    private const long MaxDragTempCopyBytes = 50L * 1024 * 1024;

    private static readonly string DragTempDir =
        Path.Combine(Path.GetTempPath(), "ClipYourself", "drag");

    private readonly StorageService _storage;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _statusTimer;
    private DateTime _suppressCaptureUntil = DateTime.MinValue;

    private Drawer? _openDrawer;
    private bool _isSettingsOpen;
    private string _statusText = string.Empty;
    private bool _hotkeyRegistered = true;
    private string _searchText = string.Empty;

    public AppSettings Settings { get; }

    /// <summary>The drawer automatically created for this session; new clips land here by default.</summary>
    public Drawer SessionDrawer { get; }

    /// <summary>All other drawers (previous sessions, projects, categories…).</summary>
    public ObservableCollection<Drawer> Drawers { get; } = new();

    public MainViewModel(StorageService storage)
    {
        _storage = storage;
        Settings = storage.LoadSettings();

        // Files handed out via drag are disposable copies; sweep leftovers from prior runs.
        try { if (Directory.Exists(DragTempDir)) Directory.Delete(DragTempDir, true); } catch { }

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
        TogglePinCommand = new RelayCommand(p => { if (p is ClipItem c) TogglePin(c); });
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
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
    public RelayCommand TogglePinCommand { get; }
    public RelayCommand ClearSearchCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            Raise(nameof(SearchText));
            Raise(nameof(IsSearching));
            Raise(nameof(SearchResults));
        }
    }

    public bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

    /// <summary>Matches across every drawer (session included), newest first.</summary>
    public List<SearchHit> SearchResults
    {
        get
        {
            if (!IsSearching) return new List<SearchHit>();
            var needle = _searchText.Trim();
            var hits = new List<SearchHit>();
            foreach (var drawer in AllDrawers())
            foreach (var clip in drawer.Clips)
            {
                if (Matches(clip, needle)) hits.Add(new SearchHit(clip, drawer.Name));
            }
            return hits.OrderByDescending(h => h.Clip.LastCopiedAt).Take(200).ToList();
        }
    }

    private static bool Matches(ClipItem clip, string needle)
    {
        if (clip.PreviewText.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
        if (clip.Text != null && clip.Text.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
        return clip.FilePaths.Any(p => Path.GetFileName(p).Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

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

    private void TogglePin(ClipItem clip)
    {
        clip.Pinned = !clip.Pinned;
        var owner = FindOwner(clip);
        if (owner != null) DrawerOps.Reposition(owner, clip);
        ScheduleSave();
    }

    // ----- drag & drop -----

    /// <summary>Files dropped onto the sidebar (or onto a specific drawer row).</summary>
    public void AddDroppedFiles(string[] paths, Drawer? target)
    {
        target ??= OpenDrawer ?? SessionDrawer;
        var added = 0;
        foreach (var path in paths)
        {
            var item = ClipCapture.FromFile(_storage, path);
            if (item == null) continue;
            DrawerOps.AddClip(target, item);
            added++;
        }
        if (added > 0)
        {
            ShowStatus(added == 1 ? $"Clipped into {target.Name}" : $"{added} clips added to {target.Name}");
            ScheduleSave();
        }
    }

    public void AddDroppedText(string text, Drawer? target)
    {
        var item = ClipCapture.FromText(text);
        if (item == null) return;
        target ??= OpenDrawer ?? SessionDrawer;
        DrawerOps.AddClip(target, item);
        ShowStatus($"Clipped into {target.Name}");
        ScheduleSave();
    }

    /// <summary>
    /// Builds the DataObject for dragging a clip OUT of the sidebar. Besides the
    /// internal filing format, it carries shell-pasteable content: a friendly-named
    /// temp file (CF_HDROP), plus text / bitmap / wave data where applicable.
    /// Drops are copy-only, so targets can never relocate blobs or originals.
    /// </summary>
    public DataObject BuildDragData(ClipItem clip)
    {
        var data = new DataObject();
        data.SetData(ClipDragFormat, clip.Id);
        try
        {
            switch (clip.Kind)
            {
                case ClipKind.Text:
                    var text = clip.Text ?? clip.PreviewText;
                    data.SetText(text);
                    data.SetFileDropList(FileList(WriteDragFile(
                        clip.Id, MakeFileName(clip.PreviewText, ".txt"), Encoding.UTF8.GetBytes(text))));
                    break;

                case ClipKind.Image:
                    if (clip.ImagePath != null && File.Exists(clip.ImagePath))
                    {
                        var imageName = clip.FilePaths.Count > 0
                            ? Path.GetFileName(clip.FilePaths[0])
                            : MakeFileName("clip-image", Path.GetExtension(clip.ImagePath));
                        data.SetFileDropList(FileList(CopyDragFile(clip.Id, clip.ImagePath, imageName)));
                        data.SetImage(ClipboardWriter.LoadBitmap(clip.ImagePath));
                    }
                    break;

                case ClipKind.Audio:
                    var source = clip.AudioPath != null && File.Exists(clip.AudioPath)
                        ? clip.AudioPath
                        : clip.FilePaths.FirstOrDefault(File.Exists);
                    if (source != null)
                    {
                        var info = new FileInfo(source);
                        var path = info.Length <= MaxDragTempCopyBytes
                            ? CopyDragFile(clip.Id, source, MakeFileName(clip.Text ?? "clip-audio", Path.GetExtension(source)))
                            : source;
                        data.SetFileDropList(FileList(path));
                        if (info.Length <= MaxWaveClipboardBytes &&
                            Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase))
                        {
                            data.SetAudio(File.ReadAllBytes(path));
                        }
                    }
                    break;

                case ClipKind.Files:
                    var existing = clip.FilePaths.Where(File.Exists).ToArray();
                    if (existing.Length > 0)
                    {
                        var list = new System.Collections.Specialized.StringCollection();
                        list.AddRange(existing);
                        data.SetFileDropList(list);
                    }
                    break;
            }
        }
        catch
        {
            // Fall back to an internal-only drag rather than failing the gesture.
        }
        return data;
    }

    private static string WriteDragFile(string clipId, string fileName, byte[] bytes)
    {
        var dir = Path.Combine(DragTempDir, clipId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string CopyDragFile(string clipId, string sourcePath, string fileName)
    {
        var dir = Path.Combine(DragTempDir, clipId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path)) File.Copy(sourcePath, path);
        return path;
    }

    private static System.Collections.Specialized.StringCollection FileList(string path)
    {
        var list = new System.Collections.Specialized.StringCollection { path };
        return list;
    }

    private static string MakeFileName(string basis, string extension)
    {
        var name = new string(basis.Trim()
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '\n' || c == '\r' ? '_' : c)
            .ToArray());
        if (name.Length > 40) name = name[..40].Trim();
        if (name.Length == 0) name = "clip";
        if (string.IsNullOrEmpty(extension)) extension = ".dat";
        return name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name : name + extension;
    }

    /// <summary>Files a clip into another drawer (drag-to-drawer). Dedup merges if the target already has it.</summary>
    public void MoveClipToDrawer(string clipId, Drawer target)
    {
        var owner = AllDrawers().FirstOrDefault(d => d.Clips.Any(c => c.Id == clipId));
        var clip = owner?.Clips.FirstOrDefault(c => c.Id == clipId);
        if (owner == null || clip == null || owner == target) return;

        owner.Clips.Remove(clip);
        DrawerOps.AddClip(target, clip);
        ShowStatus($"Moved to {target.Name}");
        ScheduleSave();
    }

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

    private void OnAnyClipsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleSave();
        if (IsSearching) Raise(nameof(SearchResults));
    }

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

/// <summary>A search match paired with the name of the drawer it lives in.</summary>
public class SearchHit
{
    public SearchHit(ClipItem clip, string drawerName)
    {
        Clip = clip;
        DrawerName = drawerName;
    }

    public ClipItem Clip { get; }
    public string DrawerName { get; }
}
