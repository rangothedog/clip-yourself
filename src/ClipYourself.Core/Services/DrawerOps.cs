using ClipYourself.Core.Models;

namespace ClipYourself.Core.Services;

/// <summary>
/// Drawer mutation rules. Invariant maintained throughout: pinned clips form a
/// contiguous block at the top of the drawer, newest-first below them.
/// </summary>
public static class DrawerOps
{
    /// <summary>
    /// Adds a clip with smart dedup: an identical clip is moved back to the top
    /// (or left in place if pinned) instead of being duplicated. Enforces the
    /// drawer's limits by evicting the oldest unpinned clips.
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
            MoveToTopOfUnpinned(drawer, existingIndex);
            return evicted;
        }

        drawer.Clips.Insert(LeadingPinnedCount(drawer), item);

        while (drawer.Clips.Count > drawer.MaxClips)
        {
            if (!TryEvictOldest(drawer, evicted)) break;
        }

        while (drawer.TotalSizeBytes > drawer.MaxSizeBytes && CountUnpinned(drawer) > 1)
        {
            if (!TryEvictOldest(drawer, evicted)) break;
        }

        return evicted;
    }

    /// <summary>Refresh a clip's timestamp and bubble it to the top of the unpinned block.</summary>
    public static void Touch(Drawer drawer, ClipItem item)
    {
        var index = drawer.Clips.IndexOf(item);
        if (index < 0) return;
        item.LastCopiedAt = DateTime.Now;
        MoveToTopOfUnpinned(drawer, index);
    }

    /// <summary>Re-home a clip after its Pinned flag changed, keeping the pinned block contiguous.</summary>
    public static void Reposition(Drawer drawer, ClipItem item)
    {
        var index = drawer.Clips.IndexOf(item);
        if (index < 0) return;
        if (item.Pinned)
        {
            if (index != 0) drawer.Clips.Move(index, 0);
        }
        else
        {
            MoveToTopOfUnpinned(drawer, index);
        }
    }

    private static void MoveToTopOfUnpinned(Drawer drawer, int index)
    {
        if (drawer.Clips[index].Pinned) return;
        var target = 0;
        while (target < drawer.Clips.Count && target != index && drawer.Clips[target].Pinned) target++;
        if (target != index) drawer.Clips.Move(index, target);
    }

    private static int LeadingPinnedCount(Drawer drawer)
    {
        var count = 0;
        foreach (var clip in drawer.Clips)
        {
            if (clip.Pinned) count++;
            else break;
        }
        return count;
    }

    private static int CountUnpinned(Drawer drawer)
        => drawer.Clips.Count(c => !c.Pinned);

    private static bool TryEvictOldest(Drawer drawer, List<ClipItem> evicted)
    {
        for (var i = drawer.Clips.Count - 1; i >= 0; i--)
        {
            if (!drawer.Clips[i].Pinned)
            {
                evicted.Add(drawer.Clips[i]);
                drawer.Clips.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
}
