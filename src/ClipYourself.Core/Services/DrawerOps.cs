using ClipYourself.Core.Models;

namespace ClipYourself.Core.Services;

public static class DrawerOps
{
    /// <summary>
    /// Adds a clip to a drawer with smart dedup: if an identical clip already exists
    /// it is moved to the top and its timestamp refreshed instead of being duplicated.
    /// Enforces the drawer's max-clip and max-size limits by evicting the oldest clips.
    /// </summary>
    /// <returns>The clips that were evicted (so callers can clean up blobs).</returns>
    public static List<ClipItem> AddClip(Drawer drawer, ClipItem item)
    {
        var evicted = new List<ClipItem>();

        var existingIndex = -1;
        for (var i = 0; i < drawer.Clips.Count; i++)
        {
            if (drawer.Clips[i].Hash == item.Hash) { existingIndex = i; break; }
        }

        if (existingIndex >= 0)
        {
            var existing = drawer.Clips[existingIndex];
            existing.LastCopiedAt = DateTime.Now;
            if (existingIndex != 0) drawer.Clips.Move(existingIndex, 0);
            return evicted;
        }

        drawer.Clips.Insert(0, item);

        while (drawer.Clips.Count > drawer.MaxClips)
        {
            evicted.Add(RemoveLast(drawer));
        }

        while (drawer.Clips.Count > 1 && drawer.TotalSizeBytes > drawer.MaxSizeBytes)
        {
            evicted.Add(RemoveLast(drawer));
        }

        return evicted;
    }

    public static void Touch(Drawer drawer, ClipItem item)
    {
        var index = drawer.Clips.IndexOf(item);
        if (index < 0) return;
        item.LastCopiedAt = DateTime.Now;
        if (index != 0) drawer.Clips.Move(index, 0);
    }

    private static ClipItem RemoveLast(Drawer drawer)
    {
        var last = drawer.Clips[^1];
        drawer.Clips.RemoveAt(drawer.Clips.Count - 1);
        return last;
    }
}
