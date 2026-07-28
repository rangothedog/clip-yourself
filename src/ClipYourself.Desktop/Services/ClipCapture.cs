using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using ClipYourself.Core.Models;
using ClipYourself.Core.Services;

namespace ClipYourself.Desktop.Services;

/// <summary>Reads the current clipboard contents and turns them into a ClipItem.</summary>
public static class ClipCapture
{
    private const int MaxTextLength = 500_000;
    private const int PreviewLength = 400;
    private const long MaxImportBytes = 200L * 1024 * 1024;

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".aac", ".wma", ".flac", ".ogg", ".opus", ".aiff", ".aif", ".mka", ".weba" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    public static ClipItem? TryCapture(StorageService storage)
    {
        // The clipboard is a shared resource; another process may hold it open briefly.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return CaptureCore(storage);
            }
            catch (COMException) { Thread.Sleep(80); }
            catch (ExternalException) { Thread.Sleep(80); }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public static bool IsAudioFile(string path)
        => AudioExtensions.Contains(Path.GetExtension(path));

    /// <summary>True for files the sidebar gives rich previews (waveform / thumbnail).</summary>
    public static bool IsMediaFile(string path)
    {
        var ext = Path.GetExtension(path);
        return AudioExtensions.Contains(ext) || ImageExtensions.Contains(ext);
    }

    /// <summary>Builds a clip from a file dropped onto the sidebar (bypasses the clipboard).</summary>
    public static ClipItem? FromFile(StorageService storage, string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var ext = Path.GetExtension(path);
            if (AudioExtensions.Contains(ext)) return CaptureAudioFile(storage, path);
            if (ImageExtensions.Contains(ext)) return CaptureImageFile(storage, path);
            return FromPathList(new List<string> { path });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>One bundled Files clip for a set of files and/or folders.</summary>
    public static ClipItem? FromPathList(List<string> paths)
    {
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (existing.Count == 0) return null;
        return CaptureFileList(existing);
    }

    /// <summary>Builds a clip from text dropped onto the sidebar.</summary>
    public static ClipItem? FromText(string text)
        => string.IsNullOrWhiteSpace(text) ? null : CaptureText(text);

    private static ClipItem? CaptureCore(StorageService storage)
    {
        if (Clipboard.ContainsFileDropList())
        {
            var paths = Clipboard.GetFileDropList().Cast<string>()
                .Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
            if (paths.Count == 0) return null;

            if (paths.Count == 1 && File.Exists(paths[0]))
            {
                var ext = Path.GetExtension(paths[0]);
                if (AudioExtensions.Contains(ext)) return CaptureAudioFile(storage, paths[0]);
                if (ImageExtensions.Contains(ext)) return CaptureImageFile(storage, paths[0]);
            }
            return CaptureFileList(paths);
        }

        if (Clipboard.ContainsAudio())
        {
            var audio = CaptureAudioStream(storage);
            if (audio != null) return audio;
        }

        if (Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image != null) return CaptureBitmap(storage, image);
        }

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(text)) return CaptureText(text);
        }

        return null;
    }

    private static ClipItem CaptureText(string text)
    {
        if (text.Length > MaxTextLength) text = text[..MaxTextLength];
        return new ClipItem
        {
            Kind = ClipKind.Text,
            Text = text,
            PreviewText = MakePreview(text),
            Hash = ClipHasher.HashText(text),
            SizeBytes = text.Length * 2L,
            LastCopiedAt = DateTime.Now
        };
    }

    private static ClipItem CaptureBitmap(StorageService storage, BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        var bytes = stream.ToArray();
        var hash = ClipHasher.HashBytes(bytes);

        return new ClipItem
        {
            Kind = ClipKind.Image,
            PreviewText = $"{image.PixelWidth} × {image.PixelHeight} image",
            ImagePath = storage.SaveBlob(bytes, hash, ".png"),
            Hash = hash,
            SizeBytes = bytes.LongLength,
            LastCopiedAt = DateTime.Now
        };
    }

    private static ClipItem CaptureImageFile(StorageService storage, string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = ClipHasher.HashBytes(bytes);
        return new ClipItem
        {
            Kind = ClipKind.Image,
            PreviewText = Path.GetFileName(path),
            ImagePath = storage.SaveBlob(bytes, hash, Path.GetExtension(path).ToLowerInvariant()),
            FilePaths = { path },
            Hash = hash,
            SizeBytes = bytes.LongLength,
            LastCopiedAt = DateTime.Now
        };
    }

    /// <summary>Raw CF_WAVE audio placed on the clipboard (sound recorders, some editors).</summary>
    private static ClipItem? CaptureAudioStream(StorageService storage)
    {
        using var stream = Clipboard.GetAudioStream();
        if (stream == null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0) return null;

        var hash = ClipHasher.HashBytes(bytes);
        return new ClipItem
        {
            Kind = ClipKind.Audio,
            Text = "Clipboard audio",
            PreviewText = "Clipboard audio",
            AudioPath = storage.SaveBlob(bytes, hash, ".wav"),
            Hash = hash,
            SizeBytes = bytes.LongLength,
            LastCopiedAt = DateTime.Now
        };
    }

    private static ClipItem CaptureAudioFile(StorageService storage, string path)
    {
        var info = new FileInfo(path);
        var hash = ClipHasher.HashFile(path);
        // Copying one of our own blobs back out shouldn't surface the hash filename.
        var displayName = path.StartsWith(storage.BlobsDir, StringComparison.OrdinalIgnoreCase)
            ? "Clipboard audio"
            : Path.GetFileName(path);
        // Copy into the blob store so the clip still plays if the original moves;
        // oversized files are referenced in place instead.
        var blobPath = info.Length <= MaxImportBytes ? storage.ImportBlob(path, hash) : path;

        return new ClipItem
        {
            Kind = ClipKind.Audio,
            Text = displayName,
            PreviewText = displayName,
            AudioPath = blobPath,
            FilePaths = { path },
            Hash = hash,
            SizeBytes = info.Length,
            LastCopiedAt = DateTime.Now
        };
    }

    private static ClipItem CaptureFileList(List<string> paths)
    {
        var names = paths.Select(p => Directory.Exists(p)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(p)) + "\\"
            : Path.GetFileName(p));
        var joined = string.Join(", ", names);
        var preview = paths.Count > 1 ? $"{paths.Count} items — {joined}" : joined;
        return new ClipItem
        {
            Kind = ClipKind.Files,
            PreviewText = MakePreview(preview),
            FilePaths = paths,
            // Order-independent hash so re-copying the same selection dedups.
            Hash = ClipHasher.HashPaths(paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)),
            SizeBytes = 0,
            LastCopiedAt = DateTime.Now
        };
    }

    private static string MakePreview(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= PreviewLength ? trimmed : trimmed[..PreviewLength] + "…";
    }
}
