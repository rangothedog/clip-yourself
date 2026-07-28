using System.Text.Json;
using ClipYourself.Core.Models;

namespace ClipYourself.Core.Services;

/// <summary>
/// File-based persistence under %LOCALAPPDATA%\ClipYourself.
/// Drawers are JSON files; image/audio payloads live in a content-addressed
/// blob store (file name = content hash) so deduped clips share storage.
/// </summary>
public class StorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public string RootDir { get; }
    public string DrawersDir { get; }
    public string BlobsDir { get; }

    public StorageService(string? rootDir = null)
    {
        RootDir = rootDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipYourself");
        DrawersDir = Path.Combine(RootDir, "drawers");
        BlobsDir = Path.Combine(RootDir, "blobs");
        Directory.CreateDirectory(DrawersDir);
        Directory.CreateDirectory(BlobsDir);
    }

    private string SettingsPath => Path.Combine(RootDir, "settings.json");

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings();
        }
        catch { /* corrupt settings fall back to defaults */ }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
        => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));

    public List<Drawer> LoadDrawers()
    {
        var drawers = new List<Drawer>();
        foreach (var file in Directory.EnumerateFiles(DrawersDir, "*.json"))
        {
            try
            {
                var drawer = JsonSerializer.Deserialize<Drawer>(File.ReadAllText(file), JsonOptions);
                if (drawer != null && drawer.Clips.Count > 0) drawers.Add(drawer);
            }
            catch { /* skip corrupt drawer files */ }
        }
        return drawers.OrderByDescending(d => d.CreatedAt).ToList();
    }

    public void SaveDrawer(Drawer drawer)
    {
        if (drawer.Clips.Count == 0)
        {
            DeleteDrawerFile(drawer.Id);
            return;
        }
        File.WriteAllText(DrawerPath(drawer.Id), JsonSerializer.Serialize(drawer, JsonOptions));
    }

    public void DeleteDrawerFile(string drawerId)
    {
        var path = DrawerPath(drawerId);
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteAllDrawerFiles()
    {
        foreach (var file in Directory.EnumerateFiles(DrawersDir, "*.json"))
        {
            try { File.Delete(file); } catch { }
        }
    }

    private string DrawerPath(string drawerId) => Path.Combine(DrawersDir, drawerId + ".json");

    /// <summary>Stores bytes as a content-addressed blob, returning the absolute path.</summary>
    public string SaveBlob(byte[] data, string hash, string extension)
    {
        var path = Path.Combine(BlobsDir, hash + extension);
        if (!File.Exists(path)) File.WriteAllBytes(path, data);
        return path;
    }

    /// <summary>Copies an external file into the blob store, returning the blob path.</summary>
    public string ImportBlob(string sourceFile, string hash)
    {
        var path = Path.Combine(BlobsDir, hash + Path.GetExtension(sourceFile).ToLowerInvariant());
        if (!File.Exists(path)) File.Copy(sourceFile, path);
        return path;
    }

    /// <summary>
    /// Deletes blobs no longer referenced by any live drawer. Blobs are shared
    /// between deduped clips, so cleanup must consider every drawer at once.
    /// </summary>
    public void SweepOrphanBlobs(IEnumerable<Drawer> liveDrawers)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drawer in liveDrawers)
        foreach (var clip in drawer.Clips)
        {
            if (clip.ImagePath != null) referenced.Add(Path.GetFileName(clip.ImagePath));
            if (clip.AudioPath != null) referenced.Add(Path.GetFileName(clip.AudioPath));
        }

        foreach (var file in Directory.EnumerateFiles(BlobsDir))
        {
            if (!referenced.Contains(Path.GetFileName(file)))
            {
                try { File.Delete(file); } catch { /* in use; sweep again next time */ }
            }
        }
    }
}
