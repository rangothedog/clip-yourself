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

    /// <summary>Custom drag format used to reorder drawers by dragging one onto another.</summary>
    public const string DrawerDragFormat = "ClipYourself.DrawerId";

    /// <summary>Practical CF_WAVE ceiling — Windows rejects large in-RAM wave payloads.</summary>
    private const long MaxWaveClipboardBytes = 25L * 1024 * 1024;

    private const long MaxDragTempCopyBytes = 50L * 1024 * 1024;

    private static readonly string DragTempDir =
        Path.Combine(Path.GetTempPath(), "ClipYourself", "drag");

    private readonly StorageService _storage;
    private readonly DawBridgeService _dawBridge;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _statusTimer;
    private DateTime _suppressCaptureUntil = DateTime.MinValue;

    private Drawer? _openDrawer;
    private bool _isSettingsOpen;
    private string _statusText = string.Empty;
    private bool _hotkeyRegistered = true;
    private string _searchText = string.Empty;
    private bool _isCapturePaused;
    private ClipItem? _selectionAnchor;

    public AppSettings Settings { get; }

    /// <summary>The drawer automatically created for this session; new clips land here by default.</summary>
    public Drawer SessionDrawer { get; }

    /// <summary>All other drawers (previous sessions, projects, categories…).</summary>
    public ObservableCollection<Drawer> Drawers { get; } = new();

    /// <summary>DAW Bridge: folders being watched for exported audio.</summary>
    public ObservableCollection<string> WatchedFolders { get; } = new();

    /// <summary>Set by App: re-registers the global hotkey, returns success.</summary>
    public Func<string, bool>? RebindHotkey { get; set; }

    /// <summary>Set by MainWindow: switches between AppBar dock and floating.</summary>
    public Action<bool>? SetDockMode { get; set; }

    /// <summary>Set by MainWindow: focuses the reel's name box so a fresh drawer can be renamed immediately.</summary>
    public Action? BeginRenameDrawer { get; set; }

    public MainViewModel(StorageService storage)
    {
        _storage = storage;
        Settings = storage.LoadSettings();

        // Files handed out via drag are disposable copies; sweep leftovers from prior runs.
        try { if (Directory.Exists(DragTempDir)) Directory.Delete(DragTempDir, true); } catch { }

        var loaded = storage.LoadDrawers(out var loadErrors);
        // Never sweep after a bad load — a quarantined drawer's blobs must survive.
        if (!loadErrors) storage.SweepOrphanBlobs(loaded);
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
        PasteCommand = new RelayCommand(_ => PasteIntoCurrentDrawer());
        ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
        ExitCommand = new RelayCommand(_ => App.ExitApp());
        TogglePinCommand = new RelayCommand(p => { if (p is ClipItem c) TogglePin(c); });
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
        AddWatchedFolderCommand = new RelayCommand(_ => AddWatchedFolder());
        RemoveWatchedFolderCommand = new RelayCommand(p => { if (p is string folder) RemoveWatchedFolder(folder); });

        foreach (var folder in Settings.WatchedFolders) WatchedFolders.Add(folder);
        _dawBridge = new DawBridgeService(OnBridgeAudioReady);
        _dawBridge.UpdateFolders(WatchedFolders);
    }

    /// <summary>Called on app exit; stops the folder watchers.</summary>
    public void Shutdown() => _dawBridge.Dispose();

    public RelayCommand NewDrawerCommand { get; }
    public RelayCommand OpenDrawerCommand { get; }
    public RelayCommand CloseReelCommand { get; }
    public RelayCommand DeleteDrawerCommand { get; }
    public RelayCommand ClearDrawerCommand { get; }
    public RelayCommand DeleteClipCommand { get; }
    public RelayCommand CopyClipCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand ToggleSettingsCommand { get; }
    public RelayCommand ExitCommand { get; }
    public RelayCommand TogglePinCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand AddWatchedFolderCommand { get; }
    public RelayCommand RemoveWatchedFolderCommand { get; }

    /// <summary>The global show/hide combo, e.g. "Ctrl+Alt+V".</summary>
    public string HotkeyGesture
    {
        get => Settings.Hotkey;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || Settings.Hotkey == value) return;
            Settings.Hotkey = value;
            Raise(nameof(HotkeyGesture));
            if (RebindHotkey != null)
            {
                HotkeyRegistered = RebindHotkey(value);
                ShowStatus(HotkeyRegistered
                    ? $"Hotkey set to {value}"
                    : $"⚠ Could not register {value} — another app owns it");
            }
            ScheduleSave();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            ClearClipSelection(); // selection is scoped to the visible list
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
            ClearClipSelection(); // selection is scoped to the visible list
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

    /// <summary>While true, clipboard changes are ignored (drops still work).</summary>
    public bool IsCapturePaused
    {
        get => _isCapturePaused;
        set
        {
            if (_isCapturePaused == value) return;
            _isCapturePaused = value;
            Raise(nameof(IsCapturePaused));
            ShowStatus(value ? "Capture paused" : "Capture resumed");
        }
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

    public bool ReserveDesktopSpace
    {
        get => Settings.ReserveDesktopSpace;
        set
        {
            if (Settings.ReserveDesktopSpace == value) return;
            Settings.ReserveDesktopSpace = value;
            Raise(nameof(ReserveDesktopSpace));
            Raise(nameof(IsFloating));
            SetDockMode?.Invoke(value);
            ShowStatus(value ? "Docked — reserving desktop space" : "Floating — drag the header to move it");
            ScheduleSave();
        }
    }

    /// <summary>True when the sidebar floats instead of docking — the header is draggable then.</summary>
    public bool IsFloating => !Settings.ReserveDesktopSpace;

    /// <summary>Applies a new sidebar width to the window (and re-docks). Set by the view.</summary>
    public Action<double>? SetSidebarWidth { get; set; }

    public double SidebarWidth
    {
        get => Settings.SidebarWidth;
        set
        {
            var clamped = Math.Clamp(value, 300, 640);
            if (Math.Abs(Settings.SidebarWidth - clamped) < 0.5) return;
            Settings.SidebarWidth = clamped;
            Raise(nameof(SidebarWidth));
            SetSidebarWidth?.Invoke(clamped);
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
        if (IsCapturePaused) return;
        if (DateTime.Now < _suppressCaptureUntil) return;
        Capture();
    }

    public void CaptureInitial() => Capture();

    private void Capture()
    {
        var item = ClipCapture.TryCapture(_storage);
        if (item == null) return;

        var target = OpenDrawer ?? SessionDrawer;
        var result = DrawerOps.AddClip(target, item);
        ReportLimits(target, result);
        ScheduleSave();
    }

    /// <summary>
    /// Explicitly add whatever is on the clipboard to the current drawer (the paste
    /// gesture — Ctrl+V or the 📋 button). Unlike auto-capture it ignores the pause
    /// and post-copy suppress window, so clicking a clip in one drawer to copy it and
    /// then pasting into another files it across.
    /// </summary>
    public void PasteIntoCurrentDrawer()
    {
        var item = ClipCapture.TryCapture(_storage);
        if (item == null) { ShowStatus("Nothing to paste — clipboard is empty"); return; }

        var target = OpenDrawer ?? SessionDrawer;
        var result = DrawerOps.AddClip(target, item);
        ReportLimits(target, result, $"Pasted into {target.Name}");
        ScheduleSave();
    }

    /// <summary>Limit enforcement must never be silent — clips disappearing reads as a bug.</summary>
    private void ReportLimits(Drawer target, DrawerOps.AddClipResult result, string? prefix = null)
    {
        if (result.Evicted.Count > 0) _storage.SweepOrphanBlobs(AllDrawers());

        var warning =
            result.Evicted.Count > 0
                ? $"⚠ {target.Name}: {result.Evicted.Count} oldest clip{(result.Evicted.Count == 1 ? "" : "s")} evicted (limit {target.MaxSizeMB} MB / {target.MaxClips} clips)"
            : result.OverLimit
                ? $"⚠ {target.Name} is over its {target.MaxSizeMB} MB limit — raise Max MB or remove clips"
            : null;

        if (warning != null) ShowStatus(prefix == null ? warning : $"{prefix} · {warning}");
        else if (prefix != null) ShowStatus(prefix);
    }

    // ----- clip actions -----

    private void CopyClip(ClipItem clip)
    {
        // A plain click copies and collapses any multi-selection down to this clip.
        ClearClipSelection();
        _selectionAnchor = clip;
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

    // ----- multi-select -----

    /// <summary>The clips currently on screen, in display order — the context for range selection.</summary>
    private List<ClipItem> CurrentClips()
    {
        if (OpenDrawer != null) return OpenDrawer.Clips.ToList();
        if (IsSearching) return SearchResults.Select(h => h.Clip).ToList();
        return SessionDrawer.Clips.ToList();
    }

    public bool HasSelectedClips => AllDrawers().Any(d => d.Clips.Any(c => c.IsSelected));

    /// <summary>Selected clips in the current display order (so a multi-drag keeps their order).</summary>
    public List<ClipItem> SelectedClipsInOrder() => CurrentClips().Where(c => c.IsSelected).ToList();

    /// <summary>Records where a subsequent Shift-range should start (set on a plain press).</summary>
    public void SetSelectionAnchor(ClipItem clip) => _selectionAnchor = clip;

    /// <summary>Ctrl-click: flip one clip's selection and make it the new range anchor.</summary>
    public void ToggleClipSelection(ClipItem clip)
    {
        clip.IsSelected = !clip.IsSelected;
        _selectionAnchor = clip;
    }

    /// <summary>Shift-click: select every clip between the anchor and this one (inclusive).</summary>
    public void RangeSelectTo(ClipItem clip)
    {
        var list = CurrentClips();
        var anchor = _selectionAnchor != null && list.Contains(_selectionAnchor) ? _selectionAnchor : clip;
        var lo = list.IndexOf(anchor);
        var hi = list.IndexOf(clip);
        if (lo < 0 || hi < 0) return;
        if (lo > hi) (lo, hi) = (hi, lo);

        foreach (var c in list) c.IsSelected = false;
        for (var i = lo; i <= hi; i++) list[i].IsSelected = true;
        // Anchor stays put so dragging the shift further grows/shrinks from the same origin.
    }

    public void ClearClipSelection()
    {
        foreach (var drawer in AllDrawers())
        foreach (var clip in drawer.Clips)
            clip.IsSelected = false;
        _selectionAnchor = null;
    }

    /// <summary>Delete-key handler: removes every selected clip across all drawers.</summary>
    public void DeleteSelectedClips()
    {
        var selected = AllDrawers().SelectMany(d => d.Clips).Where(c => c.IsSelected).ToList();
        if (selected.Count == 0) return;

        foreach (var clip in selected)
            FindOwner(clip)?.Clips.Remove(clip);

        _selectionAnchor = null;
        _storage.SweepOrphanBlobs(AllDrawers());
        ShowStatus($"Deleted {selected.Count} clip{(selected.Count == 1 ? "" : "s")}");
        ScheduleSave();
    }

    // ----- DAW Bridge -----

    private void AddWatchedFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Watch a folder for exported audio"
        };
        if (dialog.ShowDialog() != true) return;

        var folder = dialog.FolderName;
        if (WatchedFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) return;
        WatchedFolders.Add(folder);
        SyncWatchedFolders();
        ShowStatus($"🎚 Watching {Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))} for audio");
    }

    private void RemoveWatchedFolder(string folder)
    {
        WatchedFolders.Remove(folder);
        SyncWatchedFolders();
    }

    private void SyncWatchedFolders()
    {
        Settings.WatchedFolders = WatchedFolders.ToList();
        _dawBridge.UpdateFolders(WatchedFolders);
        ScheduleSave();
    }

    private void OnBridgeAudioReady(string path)
    {
        var item = ClipCapture.FromFile(_storage, path);
        if (item == null) return;
        var target = OpenDrawer ?? SessionDrawer;
        var result = DrawerOps.AddClip(target, item);
        ReportLimits(target, result, $"🎚 DAW Bridge: {Path.GetFileName(path)}");
        ScheduleSave();
    }

    // ----- drag & drop -----

    /// <summary>
    /// Files dropped onto the sidebar (or onto a specific drawer row).
    /// Media files become individual rich clips (waveform / thumbnail);
    /// everything else in the drop — including folders — becomes one
    /// bundled Files clip so the selection stays together.
    /// </summary>
    public void AddDroppedFiles(string[] paths, Drawer? target)
    {
        target ??= OpenDrawer ?? SessionDrawer;
        var newItems = new List<ClipItem>();
        var bundle = new List<string>();

        foreach (var path in paths)
        {
            if (File.Exists(path) && ClipCapture.IsMediaFile(path))
            {
                var item = ClipCapture.FromFile(_storage, path);
                if (item != null) newItems.Add(item);
            }
            else if (File.Exists(path) || Directory.Exists(path))
            {
                bundle.Add(path);
            }
        }

        if (bundle.Count > 0)
        {
            var item = ClipCapture.FromPathList(bundle);
            if (item != null) newItems.Add(item);
        }

        if (newItems.Count == 0) return;

        // One batch: files dropped together never evict each other.
        var result = DrawerOps.AddClipRange(target, newItems);
        var prefix = newItems.Count == 1
            ? $"Clipped into {target.Name}"
            : $"{newItems.Count} clips added to {target.Name}";
        ReportLimits(target, result, prefix);
        ScheduleSave();
    }

    /// <summary>
    /// A media URL dragged from a browser (image or video). Fetches the bytes so the
    /// image lands on the clipboard as a real image and video gets an inline preview;
    /// falls back to a text clip of the URL if the download fails or isn't media.
    /// </summary>
    public async void FetchAndAddMedia(string url, string? suggestedName, Drawer? target)
    {
        target ??= OpenDrawer ?? SessionDrawer;
        ShowStatus("⏳ Fetching media…");

        MediaFetchService.Fetched? fetched;
        try { fetched = await MediaFetchService.TryFetchAsync(url); }
        catch { fetched = null; }

        if (fetched is not { } data)
        {
            AddDroppedText(url, target); // couldn't download — keep the link
            return;
        }

        // Everything past the await runs on the UI thread; this is async void,
        // so an escaping exception would crash the app — keep it all guarded.
        try
        {
            var kind = ClassifyMedia(data.ContentType, url);
            ClipItem? item = kind switch
            {
                ClipKind.Image => ClipCapture.FromImageBytes(_storage, data.Bytes, suggestedName),
                ClipKind.Video => ClipCapture.FromVideoBytes(_storage, data.Bytes, suggestedName, MediaExtension(data.ContentType, url)),
                _ => null
            };

            if (item == null)
            {
                AddDroppedText(url, target);
                return;
            }

            var result = DrawerOps.AddClip(target, item);
            ReportLimits(target, result, $"Clipped {kind.ToString()!.ToLowerInvariant()} into {target.Name}");
            ScheduleSave();
        }
        catch
        {
            AddDroppedText(url, target);
        }
    }

    private static ClipKind? ClassifyMedia(string contentType, string url)
    {
        contentType = contentType.ToLowerInvariant();
        if (contentType.StartsWith("image/")) return ClipKind.Image;
        if (contentType.StartsWith("video/")) return ClipKind.Video;

        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        if (ClipCapture.IsImageFile(path)) return ClipKind.Image;
        if (ClipCapture.IsVideoFile(path)) return ClipKind.Video;
        return null;
    }

    private static string MediaExtension(string contentType, string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!string.IsNullOrEmpty(ext)) return ext;
        return contentType.ToLowerInvariant() switch
        {
            "video/webm" => ".webm",
            "video/quicktime" => ".mov",
            _ => ".mp4"
        };
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
    public DataObject BuildDragData(IReadOnlyList<ClipItem> clips)
    {
        var data = new DataObject();
        // Internal filing format carries every dragged clip's id (newline-separated).
        data.SetData(ClipDragFormat, string.Join("\n", clips.Select(c => c.Id)));
        try
        {
            if (clips.Count == 1)
            {
                PopulateShellPayload(data, clips[0]);
            }
            else
            {
                // Multi-drag out to the shell: hand over every file, plus joined text.
                var files = new System.Collections.Specialized.StringCollection();
                var texts = new List<string>();
                foreach (var clip in clips) CollectShellFiles(clip, files, texts);
                if (files.Count > 0) data.SetFileDropList(files);
                if (texts.Count > 0) data.SetText(string.Join(Environment.NewLine, texts));
            }
        }
        catch
        {
            // Fall back to an internal-only drag rather than failing the gesture.
        }
        return data;
    }

    /// <summary>Rich single-clip shell payload: text/bitmap/wave plus a friendly-named temp file.</summary>
    private void PopulateShellPayload(DataObject data, ClipItem clip)
    {
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

                case ClipKind.Video:
                    var vsource = clip.FilePaths.FirstOrDefault(File.Exists)
                        ?? (clip.VideoPath != null && File.Exists(clip.VideoPath) ? clip.VideoPath : null);
                    if (vsource != null)
                    {
                        var info = new FileInfo(vsource);
                        var path = info.Length <= MaxDragTempCopyBytes
                            ? CopyDragFile(clip.Id, vsource, MakeFileName(clip.Text ?? "clip-video", Path.GetExtension(vsource)))
                            : vsource;
                        data.SetFileDropList(FileList(path));
                    }
                    break;

                case ClipKind.Files:
                    var existing = clip.FilePaths.Where(p => File.Exists(p) || Directory.Exists(p)).ToArray();
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
            // Best-effort: an internal-only drag beats failing the gesture.
        }
    }

    /// <summary>Collects a clip's shell files (temp copies) and any text for an aggregated multi-drag.</summary>
    private void CollectShellFiles(ClipItem clip, System.Collections.Specialized.StringCollection files, List<string> texts)
    {
        try
        {
            switch (clip.Kind)
            {
                case ClipKind.Text:
                    var text = clip.Text ?? clip.PreviewText;
                    texts.Add(text);
                    files.Add(WriteDragFile(clip.Id, MakeFileName(clip.PreviewText, ".txt"), Encoding.UTF8.GetBytes(text)));
                    break;

                case ClipKind.Image:
                    if (clip.ImagePath != null && File.Exists(clip.ImagePath))
                    {
                        var imageName = clip.FilePaths.Count > 0
                            ? Path.GetFileName(clip.FilePaths[0])
                            : MakeFileName("clip-image", Path.GetExtension(clip.ImagePath));
                        files.Add(CopyDragFile(clip.Id, clip.ImagePath, imageName));
                    }
                    break;

                case ClipKind.Audio:
                    var source = clip.AudioPath != null && File.Exists(clip.AudioPath)
                        ? clip.AudioPath
                        : clip.FilePaths.FirstOrDefault(File.Exists);
                    if (source != null)
                    {
                        var info = new FileInfo(source);
                        files.Add(info.Length <= MaxDragTempCopyBytes
                            ? CopyDragFile(clip.Id, source, MakeFileName(clip.Text ?? "clip-audio", Path.GetExtension(source)))
                            : source);
                    }
                    break;

                case ClipKind.Video:
                    var vsource = clip.FilePaths.FirstOrDefault(File.Exists)
                        ?? (clip.VideoPath != null && File.Exists(clip.VideoPath) ? clip.VideoPath : null);
                    if (vsource != null)
                    {
                        var info = new FileInfo(vsource);
                        files.Add(info.Length <= MaxDragTempCopyBytes
                            ? CopyDragFile(clip.Id, vsource, MakeFileName(clip.Text ?? "clip-video", Path.GetExtension(vsource)))
                            : vsource);
                    }
                    break;

                case ClipKind.Files:
                    foreach (var p in clip.FilePaths.Where(p => File.Exists(p) || Directory.Exists(p)))
                        files.Add(p);
                    break;
            }
        }
        catch
        {
            // Skip a clip that can't be materialized rather than aborting the whole drag.
        }
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

    /// <summary>
    /// Reorders a clip so it lands at another clip's position (drag one clip onto
    /// another). Within a drawer it's a straight move; across drawers the clip is
    /// re-homed to the target's drawer at that slot. Handy for arranging shots.
    /// </summary>
    public void ReorderClip(string draggedClipId, ClipItem targetClip)
    {
        if (draggedClipId == targetClip.Id) return;

        var targetDrawer = AllDrawers().FirstOrDefault(d => d.Clips.Contains(targetClip));
        var sourceDrawer = AllDrawers().FirstOrDefault(d => d.Clips.Any(c => c.Id == draggedClipId));
        var dragged = sourceDrawer?.Clips.FirstOrDefault(c => c.Id == draggedClipId);
        if (targetDrawer == null || sourceDrawer == null || dragged == null) return;

        if (sourceDrawer == targetDrawer)
        {
            var from = targetDrawer.Clips.IndexOf(dragged);
            var to = targetDrawer.Clips.IndexOf(targetClip);
            if (from < 0 || to < 0 || from == to) return;
            targetDrawer.Clips.Move(from, to);
        }
        else
        {
            var to = targetDrawer.Clips.IndexOf(targetClip);
            sourceDrawer.Clips.Remove(dragged);
            targetDrawer.Clips.Insert(Math.Clamp(to, 0, targetDrawer.Clips.Count), dragged);
        }
        ScheduleSave();
    }

    /// <summary>
    /// Reorders a drawer so it lands at another drawer's slot (drag one drawer row onto
    /// another), then renumbers the list so the new order survives a restart.
    /// </summary>
    public void ReorderDrawer(string draggedDrawerId, Drawer targetDrawer)
    {
        var dragged = Drawers.FirstOrDefault(d => d.Id == draggedDrawerId);
        if (dragged == null || dragged == targetDrawer) return;

        var from = Drawers.IndexOf(dragged);
        var to = Drawers.IndexOf(targetDrawer);
        if (from < 0 || to < 0 || from == to) return;

        Drawers.Move(from, to);
        for (var i = 0; i < Drawers.Count; i++) Drawers[i].Order = i;
        ScheduleSave();
    }

    /// <summary>Files a clip into another drawer (drag-to-drawer). Dedup merges if the target already has it.</summary>
    public void MoveClipToDrawer(string clipId, Drawer target)
    {
        var owner = AllDrawers().FirstOrDefault(d => d.Clips.Any(c => c.Id == clipId));
        var clip = owner?.Clips.FirstOrDefault(c => c.Id == clipId);
        if (owner == null || clip == null || owner == target) return;

        owner.Clips.Remove(clip);
        var result = DrawerOps.AddClip(target, clip);
        ReportLimits(target, result, $"Moved to {target.Name}");
        ScheduleSave();
    }

    /// <summary>Files several clips into a drawer at once (multi-select drag). Single id defers to the rich single move.</summary>
    public void MoveClipsToDrawer(IReadOnlyList<string> clipIds, Drawer target)
    {
        if (clipIds.Count == 1) { MoveClipToDrawer(clipIds[0], target); return; }

        var moved = 0;
        foreach (var id in clipIds)
        {
            var owner = AllDrawers().FirstOrDefault(d => d.Clips.Any(c => c.Id == id));
            var clip = owner?.Clips.FirstOrDefault(c => c.Id == id);
            if (owner == null || clip == null || owner == target) continue;
            owner.Clips.Remove(clip);
            DrawerOps.AddClip(target, clip);
            clip.IsSelected = false;
            moved++;
        }

        if (moved == 0) return;
        _selectionAnchor = null;
        _storage.SweepOrphanBlobs(AllDrawers());
        ShowStatus($"Moved {moved} clip{(moved == 1 ? "" : "s")} to {target.Name}");
        ScheduleSave();
    }

    /// <summary>Drops several dragged clips at a target clip's slot. Single id defers to the straight reorder.</summary>
    public void ReorderClips(IReadOnlyList<string> clipIds, ClipItem targetClip)
    {
        if (clipIds.Count == 1) { ReorderClip(clipIds[0], targetClip); return; }

        var targetDrawer = AllDrawers().FirstOrDefault(d => d.Clips.Contains(targetClip));
        if (targetDrawer == null) return;

        foreach (var id in clipIds)
        {
            var owner = AllDrawers().FirstOrDefault(d => d.Clips.Any(c => c.Id == id));
            var clip = owner?.Clips.FirstOrDefault(c => c.Id == id);
            if (owner == null || clip == null || clip == targetClip) continue;
            owner.Clips.Remove(clip);
            var at = Math.Clamp(targetDrawer.Clips.IndexOf(targetClip), 0, targetDrawer.Clips.Count);
            targetDrawer.Clips.Insert(at, clip);
            clip.IsSelected = false;
        }
        _selectionAnchor = null;
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
        BeginRenameDrawer?.Invoke();
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
        try { _storage.SaveSettings(Settings); } catch { }
        if (!Settings.PersistClips) return;
        foreach (var drawer in AllDrawers())
        {
            // Per-drawer: one locked or failing file must not abort the rest.
            try { _storage.SaveDrawer(drawer); } catch { }
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
