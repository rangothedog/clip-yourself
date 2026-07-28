namespace ClipYourself.Core.Models;

public class AppSettings
{
    /// <summary>When true, drawers are saved to disk and restored on the next session.</summary>
    public bool PersistClips { get; set; }

    public int DefaultMaxClips { get; set; } = 200;

    public int DefaultMaxSizeMB { get; set; } = 50;

    /// <summary>Keep the sidebar above other windows.</summary>
    public bool AlwaysOnTop { get; set; } = true;

    public double SidebarWidth { get; set; } = 380;
}
