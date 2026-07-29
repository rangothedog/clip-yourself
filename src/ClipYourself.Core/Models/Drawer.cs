using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ClipYourself.Core.Models;

public class Drawer : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private int _maxClips = 200;
    private int _maxSizeMB = 250;
    private ObservableCollection<ClipItem> _clips = new();

    public Drawer()
    {
        HookClips(_clips);
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; Raise(nameof(Name)); }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>True when this drawer was auto-created at the start of a session.</summary>
    public bool IsSession { get; set; }

    public int MaxClips
    {
        get => _maxClips;
        set { if (value < 1) value = 1; if (_maxClips == value) return; _maxClips = value; Raise(nameof(MaxClips)); Raise(nameof(UsageFraction)); }
    }

    public int MaxSizeMB
    {
        get => _maxSizeMB;
        set { if (value < 1) value = 1; if (_maxSizeMB == value) return; _maxSizeMB = value; Raise(nameof(MaxSizeMB)); Raise(nameof(UsageFraction)); }
    }

    /// <summary>
    /// How full the drawer is (0–1), the higher of the clip-count and size ratios —
    /// whichever limit will trigger eviction first. Drives the usage thermometer.
    /// </summary>
    [JsonIgnore]
    public double UsageFraction
    {
        get
        {
            var clipRatio = (double)_clips.Count / Math.Max(1, _maxClips);
            var sizeRatio = (double)TotalSizeBytes / Math.Max(1, MaxSizeBytes);
            return Math.Clamp(Math.Max(clipRatio, sizeRatio), 0, 1);
        }
    }

    [JsonIgnore]
    public long MaxSizeBytes => (long)MaxSizeMB * 1024 * 1024;

    public ObservableCollection<ClipItem> Clips
    {
        get => _clips;
        set
        {
            if (ReferenceEquals(_clips, value)) return;
            if (_clips != null) _clips.CollectionChanged -= OnClipsChanged;
            _clips = value ?? new ObservableCollection<ClipItem>();
            HookClips(_clips);
            Raise(nameof(Clips));
            Raise(nameof(TotalSizeBytes));
        }
    }

    [JsonIgnore]
    public long TotalSizeBytes => _clips.Sum(c => c.SizeBytes);

    private void HookClips(ObservableCollection<ClipItem> clips)
        => clips.CollectionChanged += OnClipsChanged;

    private void OnClipsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Raise(nameof(TotalSizeBytes));
        Raise(nameof(UsageFraction));
    }

    private void Raise(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
