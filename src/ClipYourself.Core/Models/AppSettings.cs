namespace ClipYourself.Core.Models;

public class AppSettings
{
    /// <summary>When true, drawers are saved to disk and restored on the next session.</summary>
    public bool PersistClips { get; set; }

    public int DefaultMaxClips { get; set; } = 200;

    // Generous default: this is an audio-first clipboard manager and a single
    // WAV master can approach 100 MB.
    public int DefaultMaxSizeMB { get; set; } = 250;

    /// <summary>Keep the sidebar above other windows.</summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>Global show/hide hotkey, e.g. "Ctrl+Alt+V".</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+V";

    /// <summary>DAW Bridge: folders watched for newly exported audio files.</summary>
    public List<string> WatchedFolders { get; set; } = new();

    public double SidebarWidth { get; set; } = 380;
}
