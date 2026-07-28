using ClipYourself.Core.Models;

namespace ClipYourself.Core.Services;

/// <summary>
/// Drawer mutation rules. Invariant maintained throughout: pinned clips form a
/// contiguous block at the top of the drawer, newest-first below them.
/// </summary>
public static class DrawerOps
{
    public sealed class AddClipResult
    {
        public List<ClipItem> Evicted { get; } = new();

        /// <summary>True when the drawer still exceeds its limits after eviction
        /// (the incoming batch itself is bigger than the caps).</summary>
        public bool OverLimit { get; set; }
    }

    /// <summary>
    /// Adds a clip with smart dedup: an identical clip is moved back to the top
    /// (or left in place if pinned) instead of being duplicated. Enforces the
    /// drawer's limits by evicting the oldest unpinned clips.
    /// </summary>
    public static AddClipResult AddClip(Drawer drawer, ClipItem item)
        => AddClipRange(drawer, new[] { item });

    /// <summary>
    /// Adds a batch of clips. Clips from the same batch never evict each other —
    /// a multi-file drop must arrive whole even if it blows past the drawer's
    /// size cap; the result flags OverLimit so the UI can say so.
    /// </summary>
    public static AddClipResult AddClipRange(Drawer drawer, IEnumerable<ClipItem> items)
    {
        var result = new AddClipResult();
        var protectedClips = new HashSet<ClipItem>();

        foreach (var item in items)
        {
            var existingIndex = IndexOfHash(drawer, item.Hash);
            if (existingIndex >= 0)
            {
                var existing = drawer.Clips[existingIndex];
                existing.LastCopiedAt = DateTime.Now;
                MoveToTopOfUnpinned(drawer, existingIndex);
                protectedClips.Add(existing);
            }
            else
            {
                drawer.Clips.Insert(LeadingPinnedCount(drawer), item);
                protectedClips.Add(item);
            }
        }

        while (drawer.Clips.Count > drawer.MaxClips)
        {
            if (!TryEvictOldest(drawer, result.Evicted, protectedClips)) break;
        }

        while (drawer.TotalSizeBytes > drawer.MaxSizeBytes)
        {
            if (!TryEvictOldest(drawer, result.Evicted, protectedClips)) break;
        }

        result.OverLimit = drawer.Clips.Count > drawer.MaxClips
                           || drawer.TotalSizeBytes > drawer.MaxSizeBytes;
        return result;
    }

    private static int IndexOfHash(Drawer drawer, string hash)
    {
        for (var i = 0; i < drawer.Clips.Count; i++)
        {
            if (drawer.Clips[i].Hash == hash) return i;
        }
        return -1;
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

    private static bool TryEvictOldest(Drawer drawer, List<ClipItem> evicted, HashSet<ClipItem> protectedClips)
    {
        for (var i = drawer.Clips.Count - 1; i >= 0; i--)
        {
            var candidate = drawer.Clips[i];
            if (!candidate.Pinned && !protectedClips.Contains(candidate))
            {
                evicted.Add(candidate);
                drawer.Clips.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
}
