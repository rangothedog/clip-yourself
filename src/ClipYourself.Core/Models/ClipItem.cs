using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ClipYourself.Core.Models;

public class ClipItem : INotifyPropertyChanged
{
    private DateTime _lastCopiedAt;
    private bool _pinned;
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public ClipKind Kind { get; set; }

    /// <summary>Full text for Text clips (capped), display name for Audio clips.</summary>
    public string? Text { get; set; }

    /// <summary>Short preview shown in the sidebar row.</summary>
    public string PreviewText { get; set; } = string.Empty;

    /// <summary>Absolute path to the stored image blob (PNG or original format).</summary>
    public string? ImagePath { get; set; }

    /// <summary>Absolute path to the stored audio blob (or original file if too large to copy).</summary>
    public string? AudioPath { get; set; }

    /// <summary>Absolute path to the video (original file for local clips, blob for fetched ones).</summary>
    public string? VideoPath { get; set; }

    /// <summary>Original file paths for Files clips (and the source path for Audio clips).</summary>
    public List<string> FilePaths { get; set; } = new();

    /// <summary>Content hash used for duplicate detection.</summary>
    public string Hash { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Pinned clips stay at the top of their drawer and are never evicted by limits.</summary>
    public bool Pinned
    {
        get => _pinned;
        set
        {
            if (_pinned == value) return;
            _pinned = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pinned)));
        }
    }

    /// <summary>Transient multi-select state (drag/delete many at once); never persisted.</summary>
    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public DateTime LastCopiedAt
    {
        get => _lastCopiedAt;
        set
        {
            if (_lastCopiedAt == value) return;
            _lastCopiedAt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastCopiedAt)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
